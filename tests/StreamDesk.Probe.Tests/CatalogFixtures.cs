using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

internal static class CatalogFixtures
{
    public static CatalogSnapshot Create(int count = 5000) => new(
        [new("news", "Noticias"), new("music", "Música"), new("empty", "Vacía")],
        Enumerable.Range(0, count).Select(index => new XtreamChannel(index.ToString("D6"),
            index == 4999 ? "Música Águila" : index is 1 or 2 ? "Canal repetido" : $"Canal {index:D4}",
            index % 2 == 0 ? "news" : "music", "live", "ts")).ToArray(),
        new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero),
        new HashSet<string> { "000003", "000004" });
}

internal sealed class TestDatabase : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "streamdesk-f2-tests-" + Guid.NewGuid().ToString("N"));
    public string PathName => Path.Combine(Root, "streamdesk.db");
    public SqliteSettingsService Settings { get; }
    public SqliteCatalogStore Store { get; }
    public TestDatabase()
    {
        Directory.CreateDirectory(Root);
        Settings = new(PathName, pooling: false); Store = new(PathName, pooling: false);
    }
    public async Task AddProfileAsync(string id)
    {
        await Settings.InitializeAsync();
        await Settings.AddProfileAsync(new(id, id, "https://provider.invalid", Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow));
    }
    public void Execute(string sql)
    {
        using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = PathName, Pooling = false, ForeignKeys = true }.ToString());
        connection.Open(); return connection;
    }
    public void Dispose()
    {
        var resolved = Path.GetFullPath(Root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("streamdesk-f2-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test cleanup path.");
        Directory.Delete(resolved, recursive: true);
    }
}

internal sealed class FixtureProvider(CatalogSnapshot snapshot, bool failChannels = false) : IIptvProvider
{
    public bool UsesUnencryptedTransport => false;
    public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts"]));
    public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot.Categories);
    public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => failChannels
        ? Task.FromException<IReadOnlyList<XtreamChannel>>(new XtreamApiException(XtreamFailureKind.ProviderUnavailable)) : Task.FromResult(snapshot.Channels);
    public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new("https://provider.invalid/fixture");
    public void Dispose() { }
}

internal sealed class FixtureCredentials : ICredentialStore
{
    public Task SaveAsync(string key, StoredCredentials credentials, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<StoredCredentials?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<StoredCredentials?>(new("fixture-user", "fixture-password"));
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FixturePlayback : IPlaybackService
{
    public object? PlayerHandle => null;
    public bool IsReady => true;
    public bool HasActiveMedia { get; private set; }
    public PlaybackSnapshot Snapshot { get; private set; } = new(0, PlaybackState.Idle);
    public PlaybackOptions Options { get; set; } = new();
    public int Volume { get; set; } = 75;
    public bool IsMuted { get; set; }
    public event EventHandler<PlaybackSnapshot>? StateChanged;
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PlayAsync(Uri streamUri, CancellationToken cancellationToken = default)
    { HasActiveMedia = true; Snapshot = new(Snapshot.Sequence + 1, PlaybackState.Playing); StateChanged?.Invoke(this, Snapshot); return Task.CompletedTask; }
    public Task StopAsync()
    { HasActiveMedia = false; Snapshot = new(Snapshot.Sequence + 1, PlaybackState.Stopped); StateChanged?.Invoke(this, Snapshot); return Task.CompletedTask; }
    public ValueTask DisposeAsync() { HasActiveMedia = false; return ValueTask.CompletedTask; }
    public void Dispose() => HasActiveMedia = false;
}

internal sealed class FixtureLogger : IAppLogger
{
    public List<string> Events { get; } = [];
    public void LogError(string eventId, Exception exception) => Events.Add(eventId);
}
