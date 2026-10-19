using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;
using Xunit;

namespace StreamDesk.Probe.Tests;

public sealed class EncryptedSyncTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "streamdesk-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task ExportAndImportSyncPackageMergesFavoritesAndHistory()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "sync.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            var viewer = new SqliteViewerStore(dbPath, pooling: false);
            var catalogStore = new SqliteCatalogStore(dbPath, pooling: false);

            var profileId1 = "profile-home";
            var profileId2 = "profile-work";

            // Add profiles to satisfy foreign keys
            await settings.AddProfileAsync(new Profile(profileId1, "Home Profile", "https://home.invalid", "key1", DateTimeOffset.UtcNow));
            await settings.AddProfileAsync(new Profile(profileId2, "Work Profile", "https://work.invalid", "key2", DateTimeOffset.UtcNow));

            // Add channels
            var catalog = CatalogFixtures.Create(10);
            await catalogStore.ReplaceAsync(profileId1, catalog);
            await catalogStore.ReplaceAsync(profileId2, catalog);

            // In profile 1, mark channel 1 and 2 as favorite, and record channel 3 in history
            await viewer.SetFavoriteAsync(profileId1, "live", "000001", true);
            await viewer.SetFavoriteAsync(profileId1, "live", "000002", true);
            var now = DateTimeOffset.UtcNow;
            await viewer.RecordPlayedAsync(profileId1, "live", "000003", now);

            var syncService = new EncryptedSyncService(viewer);
            var syncFile = Path.Combine(root, "backup.streamdesk-sync");
            var passphrase = "SuperSecureSyncPassword123!";

            // Export from profile 1
            var exportedCount = await syncService.ExportAsync(syncFile, passphrase, profileId1);
            Assert.Equal(3, exportedCount);
            Assert.True(File.Exists(syncFile));

            // Verify header and no plaintext leakage
            var rawBytes = await File.ReadAllBytesAsync(syncFile);
            var header = Encoding.UTF8.GetString(rawBytes, 0, 18);
            Assert.Equal("STREAMDESK_SYNC_V1", header);
            var rawText = Encoding.UTF8.GetString(rawBytes);
            Assert.DoesNotContain("000001", rawText, StringComparison.Ordinal);
            Assert.DoesNotContain("000002", rawText, StringComparison.Ordinal);
            Assert.DoesNotContain("SuperSecureSyncPassword123!", rawText, StringComparison.Ordinal);

            // Import into profile 2
            var result = await syncService.ImportAsync(syncFile, passphrase, profileId2);
            Assert.Equal(2, result.FavoritesAdded);
            Assert.Equal(1, result.HistoryMerged);

            // Verify profile 2 now has the favorites and history
            var lib2 = await viewer.LoadAsync(profileId2);
            Assert.Contains("000001", lib2.Favorites);
            Assert.Contains("000002", lib2.Favorites);
            Assert.Contains(lib2.Recents, r => r.ChannelId == "000003");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportWithWrongPassphraseThrowsCryptographicException()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "sync_err.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            var viewer = new SqliteViewerStore(dbPath, pooling: false);
            var catalogStore = new SqliteCatalogStore(dbPath, pooling: false);

            var profileId = "profile-1";
            await settings.AddProfileAsync(new Profile(profileId, "Profile 1", "https://p1.invalid", "k1", DateTimeOffset.UtcNow));
            var catalog = CatalogFixtures.Create(5);
            await catalogStore.ReplaceAsync(profileId, catalog);
            await viewer.SetFavoriteAsync(profileId, "live", "channel-1", true);

            var syncService = new EncryptedSyncService(viewer);
            var syncFile = Path.Combine(root, "backup.streamdesk-sync");

            await syncService.ExportAsync(syncFile, "CorrectPassword123!", profileId);

            // Import with wrong password
            await Assert.ThrowsAsync<CryptographicException>(() =>
                syncService.ImportAsync(syncFile, "WrongPassword999!", profileId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportTamperedFileThrowsCryptographicOrInvalidDataException()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "sync_tamper.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            var viewer = new SqliteViewerStore(dbPath, pooling: false);
            var catalogStore = new SqliteCatalogStore(dbPath, pooling: false);

            var profileId = "profile-1";
            await settings.AddProfileAsync(new Profile(profileId, "Profile 1", "https://p1.invalid", "k1", DateTimeOffset.UtcNow));
            var catalog = CatalogFixtures.Create(5);
            await catalogStore.ReplaceAsync(profileId, catalog);
            await viewer.SetFavoriteAsync(profileId, "live", "channel-1", true);

            var syncService = new EncryptedSyncService(viewer);
            var syncFile = Path.Combine(root, "backup.streamdesk-sync");
            await syncService.ExportAsync(syncFile, "Passphrase123!", profileId);

            // Tamper with the ciphertext bytes
            var bytes = await File.ReadAllBytesAsync(syncFile);
            bytes[^1] ^= 0xFF; // Flip bits in ciphertext
            await File.WriteAllBytesAsync(syncFile, bytes);

            await Assert.ThrowsAsync<CryptographicException>(() =>
                syncService.ImportAsync(syncFile, "Passphrase123!", profileId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
