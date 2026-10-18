using System.Text.Json;
using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SqliteContentStore(string path, bool pooling = true, TimeProvider? clock = null) : IContentStore
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), ForeignKeys = true, Pooling = pooling, DefaultTimeout = 5 }.ToString();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public Task<MediaCatalog?> LoadCatalogAsync(string profileId, ContentKind kind, string scope = "", CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        using var connection = Open(); using var command = connection.CreateCommand(); cancellationToken.ThrowIfCancellationRequested();
        command.CommandText = "SELECT Payload FROM MediaCache WHERE ProfileId=$profile AND Kind=$kind AND Scope=$scope AND UpdatedAt >= $cutoff;";
        command.Parameters.AddWithValue("$profile", profileId); command.Parameters.AddWithValue("$kind", (int)kind); command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$cutoff", _clock.GetUtcNow().AddDays(-7).ToString("O"));
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<MediaCatalog>(json) : null;
    }, cancellationToken);

    public Task SaveCatalogAsync(string profileId, ContentKind kind, string scope, MediaCatalog catalog, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(kind) || catalog.Items.Count > 50000 || catalog.Items.Any(item => item.Kind != kind)) throw new ArgumentException("Catálogo inválido.");
        var sanitized = catalog with { Categories = catalog.Categories.Select(item => item with { Name = MediaMetadata.Text(item.Name, 512) }).ToArray(),
            Items = catalog.Items.Select(MediaMetadata.Sanitize).DistinctBy(item => item.StorageKey).ToArray() };
        var json = JsonSerializer.Serialize(sanitized);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 32 * 1024 * 1024) throw new ArgumentException("Catálogo demasiado grande.");
        using var connection = Open(); using var transaction = connection.BeginTransaction(); using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$profile", profileId); command.Parameters.AddWithValue("$kind", (int)kind); command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$date", _clock.GetUtcNow().ToString("O")); command.Parameters.AddWithValue("$json", json);
        command.CommandText = "INSERT INTO MediaCache VALUES($profile,$kind,$scope,$date,$json) ON CONFLICT(ProfileId,Kind,Scope) DO UPDATE SET UpdatedAt=excluded.UpdatedAt,Payload=excluded.Payload;";
        command.ExecuteNonQuery();
        command.Parameters.AddWithValue("$cutoff", _clock.GetUtcNow().AddDays(-7).ToString("O"));
        command.CommandText = """
            DELETE FROM MediaCache WHERE UpdatedAt < $cutoff;
            DELETE FROM MediaCache WHERE ProfileId=$profile AND rowid NOT IN (SELECT rowid FROM MediaCache WHERE ProfileId=$profile ORDER BY UpdatedAt DESC,rowid DESC LIMIT 12);
            """;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT COALESCE(SUM(length(CAST(Payload AS BLOB))),0) FROM MediaCache;";
        while (Convert.ToInt64(command.ExecuteScalar()) > 64L * 1024 * 1024)
        {
            cancellationToken.ThrowIfCancellationRequested();
            command.CommandText = "DELETE FROM MediaCache WHERE rowid IN (SELECT rowid FROM MediaCache ORDER BY UpdatedAt,rowid LIMIT 1);"; command.ExecuteNonQuery();
            command.CommandText = "SELECT COALESCE(SUM(length(CAST(Payload AS BLOB))),0) FROM MediaCache;";
        }
        cancellationToken.ThrowIfCancellationRequested(); transaction.Commit();
    }, cancellationToken);

    public Task<IReadOnlyList<MediaProgress>> LoadProgressAsync(string profileId, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<MediaProgress>>(() =>
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM MediaProgress WHERE ProfileId=$profile ORDER BY UpdatedAt DESC,ItemKey LIMIT 200;";
        command.Parameters.AddWithValue("$profile", profileId); using var reader = command.ExecuteReader(); var result = new List<MediaProgress>();
        while (reader.Read()) { cancellationToken.ThrowIfCancellationRequested(); if (JsonSerializer.Deserialize<MediaProgress>(reader.GetString(0)) is { } item) result.Add(item); }
        return result;
    }, cancellationToken);

    public Task SaveProgressAsync(string profileId, MediaProgress progress, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (progress.Item.Kind == ContentKind.Series || progress.PositionMilliseconds < 0 || progress.DurationMilliseconds < 0) throw new ArgumentException("Progreso inválido.");
        progress = progress with { Item = MediaMetadata.Sanitize(progress.Item), UpdatedAt = progress.UpdatedAt.ToUniversalTime(),
            PositionMilliseconds = progress.DurationMilliseconds > 0 ? Math.Min(progress.PositionMilliseconds, progress.DurationMilliseconds) : progress.PositionMilliseconds };
        using var connection = Open(); using var transaction = connection.BeginTransaction(); using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO MediaProgress VALUES($profile,$kind,$key,$date,$json) ON CONFLICT(ProfileId,Kind,ItemKey) DO UPDATE SET UpdatedAt=excluded.UpdatedAt,Payload=excluded.Payload; DELETE FROM MediaProgress WHERE ProfileId=$profile AND rowid NOT IN (SELECT rowid FROM MediaProgress WHERE ProfileId=$profile ORDER BY UpdatedAt DESC,rowid DESC LIMIT 200);";
        command.Parameters.AddWithValue("$profile", profileId); command.Parameters.AddWithValue("$kind", (int)progress.Item.Kind); command.Parameters.AddWithValue("$key", progress.Item.StorageKey);
        command.Parameters.AddWithValue("$date", progress.UpdatedAt.ToString("O")); command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(progress));
        command.ExecuteNonQuery(); cancellationToken.ThrowIfCancellationRequested(); transaction.Commit();
    }, cancellationToken);

    public Task<IReadOnlyList<EpgProgram>?> LoadEpgAsync(string profileId, string channelId, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<EpgProgram>?>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested(); using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM EpgCache WHERE ProfileId=$profile AND ChannelId=$channel AND UpdatedAt >= $cutoff;";
        command.Parameters.AddWithValue("$profile", profileId); command.Parameters.AddWithValue("$channel", channelId); command.Parameters.AddWithValue("$cutoff", _clock.GetUtcNow().AddMinutes(-5).ToString("O"));
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<EpgProgram[]>(json) : null;
    }, cancellationToken);

    public Task SaveEpgAsync(string profileId, string channelId, IReadOnlyList<EpgProgram> programs, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested(); using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO EpgCache VALUES($profile,$channel,$date,$json) ON CONFLICT(ProfileId,ChannelId) DO UPDATE SET UpdatedAt=excluded.UpdatedAt,Payload=excluded.Payload; DELETE FROM EpgCache WHERE UpdatedAt < $cutoff OR (ProfileId=$profile AND rowid NOT IN (SELECT rowid FROM EpgCache WHERE ProfileId=$profile ORDER BY UpdatedAt DESC,rowid DESC LIMIT 128));";
        command.Parameters.AddWithValue("$profile", profileId); command.Parameters.AddWithValue("$channel", channelId); command.Parameters.AddWithValue("$date", _clock.GetUtcNow().ToString("O"));
        command.Parameters.AddWithValue("$cutoff", _clock.GetUtcNow().AddMinutes(-5).ToString("O"));
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(programs.Where(item => item.EndUtc > item.StartUtc).Take(512).Select(item => item with
            { Title = MediaMetadata.Text(item.Title, 512), Description = MediaMetadata.Text(item.Description), StartUtc = item.StartUtc.ToUniversalTime(), EndUtc = item.EndUtc.ToUniversalTime() })));
        command.ExecuteNonQuery(); cancellationToken.ThrowIfCancellationRequested(); transaction.Commit();
    }, cancellationToken);
    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }
}
