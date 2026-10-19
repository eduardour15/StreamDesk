using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;
using Xunit;

namespace StreamDesk.Probe.Tests;

public sealed class MultipleProfilesAndConnectionsTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "streamdesk-mgmt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task CanCreateXtreamAndM3uProfilesWithIsolatedProviderType()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "test.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            var credentials = new DpapiCredentialStore(Path.Combine(root, "secrets"));
            var service = new ProfileService(settings, credentials);

            // Create Xtream profile
            await service.CreateAsync("Perfil Xtream", "https://provider.invalid:8080", "user1", "pass1");

            // Create M3U profile with local dummy file
            var m3uFile = Path.Combine(root, "channels.m3u");
            await File.WriteAllTextAsync(m3uFile, "#EXTM3U\n#EXTINF:-1,Channel 1\nhttp://stream.invalid/live.ts\n");
            await service.CreateM3uProfileAsync("Perfil M3U Local", m3uFile);

            var profiles = await service.GetProfilesAsync();
            Assert.Equal(2, profiles.Count);

            var xtreamProfile = profiles.First(p => p.Name == "Perfil Xtream");
            Assert.Equal("xtream", xtreamProfile.ProviderType);
            Assert.Equal("https://provider.invalid:8080", xtreamProfile.BaseUrl);

            var m3uProfile = profiles.First(p => p.Name == "Perfil M3U Local");
            Assert.Equal("m3u", m3uProfile.ProviderType);
            Assert.Equal(m3uFile, m3uProfile.BaseUrl);

            // Update/rename profile
            await service.UpdateAsync(xtreamProfile with { Name = "Perfil Xtream Renombrado" });
            var updated = await service.GetProfilesAsync();
            Assert.Contains(updated, p => p.Name == "Perfil Xtream Renombrado");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DisconnectingProfileStopsPlaybackAndCleansSession()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "test.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            var credentials = new DpapiCredentialStore(Path.Combine(root, "secrets"));
            var profileService = new ProfileService(settings, credentials);
            var catalogStore = new SqliteCatalogStore(dbPath, pooling: false);
            var catalogService = new CatalogService(catalogStore);

            var account = new XtreamAccount("Active", 1, 2, false, DateTimeOffset.UtcNow.AddDays(30), ["ts", "m3u8"]);
            var fixture = CatalogFixtures.Create(5);
            var providerCalls = 0;
            var provider = new AccountProvider(account, fixture);

            var playback = new PlaybackService(new ControlledBackend());

            await using var vm = new MainViewModel(
                (_, _, _) => { providerCalls++; return provider; },
                playback,
                profileService,
                new FixtureLogger(),
                catalogService,
                credentials);

            await profileService.CreateAsync("Test Profile", "https://provider.invalid", "user", "pass");
            await vm.LoadProfilesAsync();
            vm.ActiveProfile = vm.Profiles.First();

            // Connect profile
            await vm.ConnectProfileAsync();
            Assert.True(vm.IsConnected);
            Assert.NotNull(vm.ActiveConnectionsInfo);

            // Disconnect profile
            await vm.DisconnectProfileAsync();
            Assert.False(vm.IsConnected);
            Assert.Contains("Desconectado", vm.StatusMessage);
            Assert.Contains("Desconectado", vm.ActiveConnectionsInfo);
            Assert.True(playback.Snapshot.State is PlaybackState.Idle or PlaybackState.Stopped);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CheckConnectionUpdatesAccountStatusWithoutReloadingCatalog()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "test.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            var credentials = new DpapiCredentialStore(Path.Combine(root, "secrets"));
            var profileService = new ProfileService(settings, credentials);
            var catalogStore = new SqliteCatalogStore(dbPath, pooling: false);
            var catalogService = new CatalogService(catalogStore);

            var account = new XtreamAccount("Active", 2, 3, false, DateTimeOffset.UtcNow.AddDays(15), ["ts"]);
            var provider = new AccountProvider(account, CatalogFixtures.Create(5));

            var playback = new PlaybackService(new ControlledBackend());

            await using var vm = new MainViewModel(
                (_, _, _) => provider,
                playback,
                profileService,
                new FixtureLogger(),
                catalogService,
                credentials);

            await profileService.CreateAsync("Check Conn Profile", "https://provider.invalid", "user", "pass");
            await vm.LoadProfilesAsync();
            vm.ActiveProfile = vm.Profiles.First();

            await vm.CheckConnectionAsync();
            Assert.Contains("2 de 3", vm.ActiveConnectionsInfo);
            Assert.Contains("cuenta activa", vm.StatusMessage);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class AccountProvider(XtreamAccount account, CatalogSnapshot snapshot) : IIptvProvider
    {
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(account);
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot.Categories);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot.Channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new($"https://provider.invalid/live/{channel.Id}.{format}");
        public void Dispose() { }
    }
}
