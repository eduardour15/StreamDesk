using System.Text;
using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class ProfileStorageTests
{
    [Fact]
    public async Task MigrationCreatesVersionedTablesAndStoresNoCredentials()
    {
        var root = CreateTempDirectory();
        try
        {
            var database = Path.Combine(root, "streamdesk.db");
            var settings = new SqliteSettingsService(database, pooling: false);
            await settings.InitializeAsync();
            await settings.InitializeAsync();
            await settings.AddProfileAsync(new Profile("profile-1", "Test", "https://provider.invalid", "profile-1", DateTimeOffset.UtcNow));

            var profiles = await settings.GetProfilesAsync();
            Assert.Single(profiles);
            Assert.Equal("https://provider.invalid", profiles[0].BaseUrl);

            var data = await File.ReadAllBytesAsync(database);
            var contents = Encoding.UTF8.GetString(data);
            Assert.DoesNotContain("user-fixture", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("password-fixture", contents, StringComparison.Ordinal);

            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Version FROM SchemaVersion;";
            Assert.Equal(2L, (long)(await command.ExecuteScalarAsync())!);
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Profiles','Categories','Channels','Favorites','PlaybackHistory');";
            Assert.Equal(5L, (long)(await command.ExecuteScalarAsync())!);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task DpapiCredentialStoreEncryptsAndDeletesCurrentUserSecret()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new DpapiCredentialStore(root);
            var key = Guid.NewGuid().ToString("N");
            await store.SaveAsync(key, new StoredCredentials("user-fixture", "password-fixture"));
            var path = Path.Combine(root, key + ".dpapi");
            var bytes = await File.ReadAllBytesAsync(path);
            var contents = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("user-fixture", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("password-fixture", contents, StringComparison.Ordinal);
            Assert.Equal(new StoredCredentials("user-fixture", "password-fixture"), await store.GetAsync(key));

            await store.DeleteAsync(key);

            Assert.False(File.Exists(path));
            Assert.Null(await store.GetAsync(key));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ProfileServiceCreatesAndDeletesMetadataAndProtectedSecretTogether()
    {
        var root = CreateTempDirectory();
        try
        {
            var settings = new SqliteSettingsService(Path.Combine(root, "streamdesk.db"), pooling: false);
            var credentials = new DpapiCredentialStore(Path.Combine(root, "secrets"));
            await settings.InitializeAsync();
            var profiles = new ProfileService(settings, credentials);
            await profiles.CreateAsync("Fixture", "https://provider.invalid", "user-fixture", "password-fixture");
            var created = Assert.Single(await settings.GetProfilesAsync());
            Assert.Equal(new StoredCredentials("user-fixture", "password-fixture"), await credentials.GetAsync(created.CredentialKey));

            await profiles.DeleteAsync(created.Id);

            Assert.Empty(await settings.GetProfilesAsync());
            Assert.Null(await credentials.GetAsync(created.CredentialKey));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("https://user:password@provider.invalid")]
    [InlineData("https://provider.invalid?token=secret")]
    [InlineData("file:///tmp")]
    public async Task ProfileServiceRejectsCredentialBearingOrUnsupportedUrls(string url)
    {
        var root = CreateTempDirectory();
        try
        {
            var settings = new SqliteSettingsService(Path.Combine(root, "streamdesk.db"), pooling: false);
            await settings.InitializeAsync();
            var profiles = new ProfileService(settings, new DpapiCredentialStore(Path.Combine(root, "secrets")));
            await Assert.ThrowsAsync<ArgumentException>(() => profiles.CreateAsync("Fixture", url, "user-fixture", "password-fixture"));
            Assert.Empty(await settings.GetProfilesAsync());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void SafeFileLoggerOmitsExceptionMessagesAndRequestData()
    {
        var root = CreateTempDirectory();
        try
        {
            var path = Path.Combine(root, "streamdesk.log");
            var logger = new SafeFileLogger(path);
            logger.LogError("provider-failure", new InvalidOperationException("https://provider.invalid/live/user-fixture/password-fixture"));

            var log = File.ReadAllText(path);
            Assert.Contains("provider-failure InvalidOperationException", log, StringComparison.Ordinal);
            Assert.DoesNotContain("provider.invalid", log, StringComparison.Ordinal);
            Assert.DoesNotContain("user-fixture", log, StringComparison.Ordinal);
            Assert.DoesNotContain("password-fixture", log, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "streamdesk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
