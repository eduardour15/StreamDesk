using System.Text;
using System.Text.Json;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class StabilizationTests
{
    [Fact]
    public async Task BackupIsConsistentAndCredentialsRemainProtected()
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync();
        var credentials = new DpapiCredentialStore(Path.Combine(db.Root, "secrets"));
        var profiles = new StreamDesk.Application.ProfileService(db.Settings, credentials);
        await profiles.CreateAsync("Fixture", "https://provider.invalid", "fixture-backup-user", "fixture-backup-password");
        var profile = Assert.Single(await profiles.GetProfilesAsync());
        await db.Store.ReplaceAsync(profile.Id, CatalogFixtures.Create(10));
        var recovery = new LocalRecoveryService(db.Root); var backup = await recovery.CreateBackupAsync();
        var restored = new SqliteSettingsService(Path.Combine(backup, "streamdesk.db"), false);
        Assert.Equal(profile.Id, Assert.Single(await restored.GetProfilesAsync()).Id);
        var protectedBytes = await File.ReadAllBytesAsync(Path.Combine(backup, "secrets", profile.CredentialKey + ".dpapi"));
        Assert.DoesNotContain("fixture-backup-password", Encoding.UTF8.GetString(protectedBytes), StringComparison.Ordinal);
        var protectedStore = new DpapiCredentialStore(Path.Combine(backup, "secrets"));
        Assert.Equal("fixture-backup-user", (await protectedStore.GetAsync(profile.CredentialKey))?.Username);
        Assert.True(File.Exists(Path.Combine(backup, "backup.json")));
    }
    [Fact]
    public async Task FailedBackupDoesNotLeaveCompletedOrPartialSnapshots()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        await Assert.ThrowsAnyAsync<IOException>(() => new LocalRecoveryService(db.Root).CreateBackupAsync());
        Assert.Empty(Directory.GetDirectories(Path.Combine(db.Root, "backups")));
    }
    [Fact]
    public async Task LogsRotateRemainBoundedAndDiagnosticRejectsUnexpectedLines()
    {
        using var db = new TestDatabase(); var log = Path.Combine(db.Root, "logs", "streamdesk.log");
        await using (var logger = new SafeFileLogger(log, 1024))
        {
            for (var i = 0; i < 1000; i++) logger.LogError("fixture_error", new InvalidOperationException("https://provider.invalid/fixture-backup-password"));
        }
        Assert.True(new FileInfo(log).Length <= 1024); Assert.True(new FileInfo(log + ".1").Length <= 1024);
        Assert.DoesNotContain("fixture-backup-password", await File.ReadAllTextAsync(log), StringComparison.Ordinal);
        await File.AppendAllTextAsync(log, "malformed fixture-backup-password https://provider.invalid\n");
        var diagnostic = await new LocalRecoveryService(db.Root).ExportDiagnosticsAsync(); var json = await File.ReadAllTextAsync(diagnostic);
        Assert.DoesNotContain("fixture-backup-password", json, StringComparison.Ordinal); Assert.DoesNotContain("provider.invalid", json, StringComparison.Ordinal);
        Assert.NotEmpty(JsonDocument.Parse(json).RootElement.GetProperty("Events").EnumerateArray());
    }
    [Fact]
    public async Task AuthenticatedUrlCannotBePersistedAsContentIdentifier()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); var store = new SqliteContentStore(db.PathName, false);
        var item = new MediaItem("https://provider.invalid/movie/fixture-user/fixture-password/7.mp4", "Invalid", ContentKind.Movie);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => store.SaveProgressAsync("first", new(item, 1, 2, DateTimeOffset.UtcNow)));
        Assert.DoesNotContain("fixture-password", error.ToString(), StringComparison.Ordinal); Assert.Empty(await store.LoadProgressAsync("first"));
    }
    [Fact]
    public async Task RefreshRemovesOnlyObsoleteProgressAndPreservesOtherTypes()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); var store = new SqliteContentStore(db.PathName, false);
        var movie = new MediaItem("7", "Movie", ContentKind.Movie);
        var episode = new MediaItem("7", "Episode", ContentKind.Episode, SeriesId: "series", Season: 1);
        await store.SaveProgressAsync("first", new(movie, 1, 2, DateTimeOffset.UtcNow)); await store.SaveProgressAsync("first", new(episode, 1, 2, DateTimeOffset.UtcNow));
        await store.SaveCatalogAsync("first", ContentKind.Movie, "", new([], [], DateTimeOffset.UtcNow));
        Assert.Equal(ContentKind.Episode, Assert.Single(await store.LoadProgressAsync("first")).Item.Kind);
    }
}
