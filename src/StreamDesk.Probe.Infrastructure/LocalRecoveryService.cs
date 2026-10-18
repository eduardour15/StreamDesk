using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using StreamDesk.Application;

namespace StreamDesk.Probe.Infrastructure;

public sealed class LocalRecoveryService(string dataDirectory) : IRecoveryService
{
    private readonly string _root = Path.GetFullPath(dataDirectory);
    public Task<string> CreateBackupAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var backupRoot = LocalDirectory("backups");
        var pending = Path.Combine(backupRoot, ".pending-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(pending);
        var completed = Path.Combine(backupRoot, "backup-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_root, "streamdesk.db"), Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()); source.Open();
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(pending, "streamdesk.db"), Pooling = false }.ToString()); target.Open();
            source.BackupDatabase(target);
            using var command = target.CreateCommand(); command.CommandText = "SELECT CredentialKey FROM Profiles;";
            var secrets = Path.Combine(pending, "secrets"); Directory.CreateDirectory(secrets);
            using (var reader = command.ExecuteReader()) while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested(); var key = reader.GetString(0);
                if (!Guid.TryParseExact(key, "N", out _)) throw new InvalidOperationException("Clave de credencial no válida.");
                var from = Path.Combine(_root, "secrets", key + ".dpapi");
                if ((File.GetAttributes(from) & FileAttributes.ReparsePoint) != 0) throw new IOException("No se admiten enlaces en credenciales.");
                File.Copy(from, Path.Combine(secrets, key + ".dpapi"));
            }
            command.CommandText = "SELECT Version FROM SchemaVersion;"; var schema = Convert.ToInt32(command.ExecuteScalar());
            File.WriteAllText(Path.Combine(pending, "backup.json"), JsonSerializer.Serialize(new { Format = "StreamDeskBackup", Schema = schema, CreatedUtc = DateTimeOffset.UtcNow, Protection = "DPAPI CurrentUser" }));
            cancellationToken.ThrowIfCancellationRequested(); target.Close(); source.Close();
            Directory.Move(pending, completed); return completed;
        }
        catch
        {
            var resolved = Path.GetFullPath(pending);
            if (!resolved.StartsWith(backupRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith(".pending-", StringComparison.Ordinal)) throw;
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            throw;
        }
    }, cancellationToken);

    public Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested(); var directory = LocalDirectory("diagnostics");
        var events = new List<string>(); var log = Path.Combine(_root, "logs", "streamdesk.log");
        if (File.Exists(log) && new FileInfo(log).Length <= 1024 * 1024)
            events.AddRange(File.ReadLines(log).Where(line => Regex.IsMatch(line, @"^\d{4}-\d{2}-\d{2}T[0-9:.+Z-]+ [A-Za-z0-9_-]{1,40} [A-Za-z0-9_-]{1,80}$", RegexOptions.None, TimeSpan.FromSeconds(1))).TakeLast(100));
        var output = Path.Combine(directory, "diagnostic-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(output, JsonSerializer.Serialize(new { CreatedUtc = DateTimeOffset.UtcNow, OS = Environment.OSVersion.VersionString,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), Runtime = Environment.Version.ToString(), Events = events }));
        return output;
    }, cancellationToken);

    private string LocalDirectory(string name)
    {
        if ((new DirectoryInfo(_root).Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("No se admiten enlaces en datos locales.");
        var path = Path.Combine(_root, name);
        if (Directory.Exists(path) && (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("No se admiten enlaces en copias locales.");
        Directory.CreateDirectory(path); return path;
    }
}
