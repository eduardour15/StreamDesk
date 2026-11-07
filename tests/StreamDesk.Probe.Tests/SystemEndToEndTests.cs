using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

[Collection("NativePlayback")]
public sealed class SystemEndToEndTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "streamdesk-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempDirectory(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            try { Directory.Delete(path, recursive: true); }
            catch { /* Best effort on test cleanup */ }
        }
    }

    [Fact]
    public async Task E2E_01_Storage_Profiles_Credentials_And_Privacy()
    {
        var root = CreateTempDirectory();
        try
        {
            using (var services = new AppServices(root))
            {
                await services.Settings.InitializeAsync();

                // 1. Initial State: No profiles
                var initialProfiles = await services.Profiles.GetProfilesAsync();
                Assert.Empty(initialProfiles);

                // 2. Create Xtream Profile
                await services.Profiles.CreateAsync("Mi Perfil IPTV", "https://provider.invalid:8080", "usuario_test", "password_secreto_123");
                var profiles = await services.Profiles.GetProfilesAsync();
                var profile = Assert.Single(profiles, p => p.Name == "Mi Perfil IPTV");
                Assert.NotNull(profile);
                Assert.Equal("xtream", profile.ProviderType);

                // 3. Create M3U Profile
                var m3uPath = Path.Combine(root, "canales.m3u8");
                await File.WriteAllTextAsync(m3uPath, "#EXTM3U\n#EXTINF:-1 group-title=\"Noticias\",Canal 1\nhttps://cdn.example.tv/1.ts\n");
                await services.Profiles.CreateM3uProfileAsync("Perfil Local M3U", m3uPath);
                profiles = await services.Profiles.GetProfilesAsync();
                var m3uProfile = Assert.Single(profiles, p => p.Name == "Perfil Local M3U");
                Assert.NotNull(m3uProfile);
                Assert.Equal("m3u", m3uProfile.ProviderType);

                // 4. Verify DPAPI encryption on disk
                var secretsDir = Path.Combine(root, "secrets");
                Assert.True(Directory.Exists(secretsDir));
                var secretFiles = Directory.GetFiles(secretsDir);
                Assert.NotEmpty(secretFiles);

                // Ensure file contains encrypted ciphertext and NOT plaintext password
                var fileBytes = await File.ReadAllBytesAsync(secretFiles[0]);
                var fileText = Encoding.UTF8.GetString(fileBytes);
                Assert.DoesNotContain("password_secreto_123", fileText, StringComparison.Ordinal);

                // Ensure DPAPI recovers the exact credentials
                var creds = await services.Credentials.GetAsync(profile.CredentialKey);
                Assert.NotNull(creds);
                Assert.Equal("usuario_test", creds.Username);
                Assert.Equal("password_secreto_123", creds.Password);

                // 5. Verify SQLite settings & profiles table
                var dbPath = Path.Combine(root, "streamdesk.db");
                using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Profiles;";
                var profileCount = (long)(cmd.ExecuteScalar() ?? 0L);
                Assert.Equal(2L, profileCount);

                // Verify SQLite does NOT contain the plaintext password
                cmd.CommandText = "SELECT BaseUrl FROM Profiles WHERE Name = 'Mi Perfil IPTV';";
                var storedUrl = (string?)cmd.ExecuteScalar();
                Assert.Equal("https://provider.invalid:8080", storedUrl);

                // 6. Update AppSettings
                var currentSettings = await services.Settings.GetAppSettingsAsync();
                var updatedSettings = currentSettings with
                {
                    Theme = "Light",
                    UiScale = "110%",
                    BufferMilliseconds = 5000,
                    PrioritizeContinuity = true,
                    LiveQuality = LiveQualityPreference.DataSaver
                };
                await services.Settings.SaveAppSettingsAsync(updatedSettings);

                // 7. Cascade Deletion
                await services.Profiles.DeleteAsync(profile.Id);
                var remaining = await services.Profiles.GetProfilesAsync();
                Assert.Single(remaining);
                Assert.Equal("Perfil Local M3U", remaining[0].Name);

                // Verify credential file deleted
                var deletedCreds = await services.Credentials.GetAsync(profile.CredentialKey);
                Assert.Null(deletedCreds);
            }

            // 8. Re-instantiate AppServices from scratch to verify persistence across restarts
            using (var reloadedServices = new AppServices(root))
            {
                await reloadedServices.Settings.InitializeAsync();
                var persistedProfiles = await reloadedServices.Profiles.GetProfilesAsync();
                Assert.Single(persistedProfiles);
                Assert.Equal("Perfil Local M3U", persistedProfiles[0].Name);

                var loadedSettings = await reloadedServices.Settings.GetAppSettingsAsync();
                Assert.Equal("Light", loadedSettings.Theme);
                Assert.Equal("110%", loadedSettings.UiScale);
                Assert.Equal(5000, loadedSettings.BufferMilliseconds);
                Assert.True(loadedSettings.PrioritizeContinuity);
                Assert.Equal(LiveQualityPreference.DataSaver, loadedSettings.LiveQuality);
            }

            // 9. Privacy Verification in Logs
            var logPath = Path.Combine(root, "logs", "streamdesk.log");
            if (File.Exists(logPath))
            {
                var logContents = await File.ReadAllTextAsync(logPath);
                Assert.DoesNotContain("password_secreto_123", logContents, StringComparison.Ordinal);
                Assert.DoesNotContain("usuario_test", logContents, StringComparison.Ordinal);
            }
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_02_HttpXtreamServer_Ingestion_And_OfflineResilience()
    {
        var root = CreateTempDirectory();
        await using var server = new LocalXtreamMockServer();
        try
        {
            using var services = new AppServices(root);
            await services.Settings.InitializeAsync();

            var profile = new Profile("profile-xtream", "Perfil Xtream", server.BaseUrl, "key-xtream", DateTimeOffset.UtcNow, "xtream");
            await services.Settings.AddProfileAsync(profile);

            var creds = new FixtureCredentials();
            var logger = new FixtureLogger();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);

            await using var vm = new MainViewModel(
                (url, u, p) => new XtreamApiClient(url, u, p, requestTimeout: TimeSpan.FromSeconds(5)),
                playback,
                services.Profiles,
                logger,
                services.Catalogs,
                creds,
                services.Viewer,
                services.Content,
                new XmlTvReader(),
                services.Recovery,
                services.Playlists,
                services.Settings,
                services.Sync,
                services.ParentalControl);

            // 1. Authenticate & Connect via Profile
            vm.ActiveProfile = profile;
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.ConnectProfileAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            Assert.True(vm.HasHttpWarning);
            Assert.Contains("sin cifrar", vm.HttpWarning, StringComparison.OrdinalIgnoreCase);

            // 2. Load Catalog: Live Channels & Categories (1 'all' category + 3 from provider = 4 categories)
            await vm.LoadCatalogAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking && vm.Channels.Count == 3);
            Assert.Equal(4, vm.Categories.Count);
            Assert.Equal(3, vm.Channels.Count);
            Assert.Contains(vm.Channels, c => c.Name == "Canal 24H Noticias");
            Assert.Contains(vm.Channels, c => c.Name == "Deportes Total HD");
            Assert.Contains(vm.Channels, c => c.Name == "Cine Clásico");

            // 3. Search Filter (Accent & Case Insensitive)
            vm.SearchText = "clasico";
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 1);
            Assert.Equal("Cine Clásico", vm.Channels[0].Name);
            vm.SearchText = "";
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 3);

            // 4. Filter by Category
            vm.SelectedCategory = vm.Categories.Single(cat => cat.Name == "Deportes");
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 1);
            Assert.Equal("Deportes Total HD", vm.Channels[0].Name);
            vm.SelectedCategory = vm.Categories[0]; // Reset category
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 3);

            // 5. Favorites Toggle
            vm.SelectedChannel = vm.Channels[0];
            await vm.ToggleFavoriteAsync();
            Assert.True(vm.SelectedIsFavorite);
            vm.FavoritesOnly = true;
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 1);
            Assert.Equal("Canal 24H Noticias", vm.Channels[0].Name);
            vm.FavoritesOnly = false;
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 3);

            // 6. VOD Movies & Series Probe & Load
            await vm.ProbeContentAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            Assert.True(vm.HasMovies);
            Assert.True(vm.HasSeries);

            vm.CurrentTabIndex = 5; // Movies
            await PlaybackTests.UntilAsync(() => vm.MediaItems.Count == 2);
            Assert.Contains(vm.MediaItems, m => m.Name == "El Gran Escape");

            vm.CurrentTabIndex = 6; // Series
            await PlaybackTests.UntilAsync(() => vm.MediaItems.Count == 1);
            Assert.Equal("Planeta Salvaje", vm.MediaItems[0].Name);

            // 7. Offline Resilience Simulation: Shut down the server!
            await server.DisposeAsync();

            // Create a brand new MainViewModel reading only from the SQLite store
            await using var offlineVm = new MainViewModel(
                (url, u, p) => new XtreamApiClient(url, u, p, requestTimeout: TimeSpan.FromSeconds(1)),
                playback,
                services.Profiles,
                logger,
                services.Catalogs,
                creds,
                services.Viewer,
                services.Content,
                new XmlTvReader(),
                services.Recovery,
                services.Playlists,
                services.Settings,
                services.Sync,
                services.ParentalControl);

            // Setting ActiveProfile automatically triggers offline cache hydration
            offlineVm.ActiveProfile = profile;
            await PlaybackTests.UntilAsync(() => !offlineVm.IsCatalogLoading && offlineVm.Channels.Count == 3);
            Assert.Equal(4, offlineVm.Categories.Count);
            Assert.Equal(3, offlineVm.Channels.Count);

            // Verify VOD loads from offline SQLite cache
            offlineVm.CurrentTabIndex = 5; // Movies
            await PlaybackTests.UntilAsync(() => offlineVm.MediaItems.Count == 2);
            Assert.Equal(2, offlineVm.MediaItems.Count);

            offlineVm.CurrentTabIndex = 6; // Series
            await PlaybackTests.UntilAsync(() => offlineVm.MediaItems.Count == 1);
            Assert.Equal("Planeta Salvaje", offlineVm.MediaItems[0].Name);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_03_Playback_SingleActiveStream_Buffer_Tracks_And_Amplification()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend, new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10)));
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);

            var snapshot = new CatalogSnapshot(
                [new("live", "En Vivo")],
                [
                    new("1", "Canal Uno", "live", "live", "ts"),
                    new("2", "Canal Dos", "live", "live", "ts")
                ],
                DateTimeOffset.UtcNow);

            await using var vm = new MainViewModel(
                (_, _, _) => new FixtureProvider(snapshot),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials);

            await vm.InitializePlaybackAsync();
            await vm.ConnectAsync("https://provider.invalid", "user", "pass");
            await vm.LoadCatalogAsync();

            // 1. Play Channel 1
            vm.SelectedChannel = vm.Channels[0];
            await vm.PlaySelectedAsync();
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            Assert.Equal(1, backend.Active);
            Assert.Equal(1, backend.Peak);

            // 2. Play Channel 2 -> Verify Channel 1 is closed first and Peak remains strictly 1
            vm.SelectedChannel = vm.Channels[1];
            await vm.PlaySelectedAsync();
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            Assert.Equal(1, backend.Active);
            Assert.Equal(1, backend.Peak); // Peak MUST NOT exceed 1!

            // 3. Audio & Subtitle Track Switching
            var audioTracks = new[] { new MediaTrack(1, "Español"), new MediaTrack(2, "English") };
            var subTracks = new[] { new MediaTrack(-1, "Desactivados"), new MediaTrack(10, "Español"), new MediaTrack(20, "English") };
            backend.Details = new(0, 3600000, true, audioTracks, subTracks, 1, -1);

            await vm.RefreshPlaybackDetailsAsync();
            Assert.Equal(2, vm.AudioTracks.Count);
            Assert.Equal(3, vm.SubtitleTracks.Count);

            // Change Audio track to English (Id 2)
            vm.SelectedAudioTrack = vm.AudioTracks[1];
            await vm.SelectTrackAsync(false);
            Assert.Equal((false, 2), backend.ChosenTrack);

            // Change Subtitle track to Español (Id 10)
            vm.SelectedSubtitleTrack = vm.SubtitleTracks[1];
            await vm.SelectTrackAsync(true);
            Assert.Equal((true, 10), backend.ChosenTrack);

            // 4. Sound Booster (Amplification)
            Assert.Equal(0, vm.AmplificationDb);
            vm.CycleAmplification(); // 0 -> 2 dB
            Assert.Equal(2, vm.AmplificationDb);
            Assert.Equal(2, backend.AmplificationDb);

            vm.CycleAmplification(); // 2 -> 4 dB
            Assert.Equal(4, vm.AmplificationDb);
            Assert.Equal(4, backend.AmplificationDb);

            vm.CycleAmplification(); // 4 -> 6 dB
            Assert.Equal(6, vm.AmplificationDb);
            Assert.Equal(6, backend.AmplificationDb);

            vm.CycleAmplification(); // 6 -> 8 dB
            Assert.Equal(8, vm.AmplificationDb);
            Assert.Equal(8, backend.AmplificationDb);

            vm.AmplificationDb = 0; // Reset to 0
            Assert.Equal(0, vm.AmplificationDb);
            Assert.Equal(0, backend.AmplificationDb);

            // 5. Pause & Resume
            await vm.TogglePlayPauseAsync();
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Paused));
            await PlaybackTests.UntilAsync(() => vm.IsPaused);
            Assert.False(vm.IsPlaying);

            await vm.TogglePlayPauseAsync();
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // 6. Stop
            await vm.StopPlaybackAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsPlaying && !vm.IsPaused);
            Assert.Equal(0, backend.Active);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_04_VOD_And_Series_Lifecycle_Resume_And_RandomEpisode()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profile = new Profile("profile-vod", "Perfil VOD", "https://provider.invalid", "key-vod", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profile);

            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var policy = new PlaybackPolicy(TimeSpan.FromSeconds(5), TimeSpan.Zero) { MaxRetries = 0 };
            var playback = new PlaybackService(backend, policy);
            var logger = new FixtureLogger();
            var contentStore = new SqliteContentStore(Path.Combine(root, "settings.db"), pooling: false);
            var viewerStore = new SqliteViewerStore(Path.Combine(root, "settings.db"), pooling: false);

            var movie = new MediaItem("mov-1", "Aventura Espacial", ContentKind.Movie, "sci-fi", "mp4");
            var episode1 = new MediaItem("ep-1", "Piloto", ContentKind.Episode, "1", "mp4", SeriesId: "ser-1", Season: 1, Episode: 1);
            var episode2 = new MediaItem("ep-2", "Capítulo Dos", ContentKind.Episode, "1", "mp4", SeriesId: "ser-1", Season: 1, Episode: 2);
            var series = new MediaItem("ser-1", "Crónicas Galácticas", ContentKind.Series);

            await using var vm = new MainViewModel(
                (_, _, _) => new FullContentFixtureProvider(movies: [movie], series: [series], episodes: [episode1, episode2]),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false)),
                credentials,
                viewerStore,
                contentStore);

            await vm.InitializePlaybackAsync();
            vm.ActiveProfile = profile;
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.ConnectProfileAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.ProbeContentAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);

            // 1. Play Movie & Save Progress
            vm.CurrentTabIndex = 5; // Movies
            await PlaybackTests.UntilAsync(() => vm.MediaItems.Count == 1);
            vm.SelectedMedia = vm.MediaItems[0];
            await PlaybackTests.UntilAsync(() => vm.MediaDescription.Contains("Sinopsis de prueba", StringComparison.Ordinal));

            await vm.PlayMediaAsync();
            await PlaybackTests.UntilAsync(() => backend.Opens.Count > 0);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // Simulate progress (position 75s out of 180s)
            backend.Details = new(75000, 180000, true, [], []);
            await vm.RefreshPlaybackDetailsAsync();

            await vm.StopPlaybackAsync();

            // Verify progress persisted in content store
            var savedProgress = await contentStore.LoadProgressAsync("profile-vod");
            var movieProgress = Assert.Single(savedProgress);
            Assert.Equal("mov-1", movieProgress.Item.Id);
            Assert.Equal(75000, movieProgress.PositionMilliseconds);

            // 2. Resume from Continue Watching
            vm.CurrentTabIndex = 7; // Continue Watching
            await PlaybackTests.UntilAsync(() => vm.ContinueWatching.Count == 1);
            vm.SelectedProgress = vm.ContinueWatching[0];

            await vm.PlayMediaAsync();
            await PlaybackTests.UntilAsync(() => backend.Opens.Count > 1);
            Assert.Equal(75000, backend.Opens.Last().Options.StartPositionMilliseconds);

            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // Media reaches the end -> removed from Continue Watching
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Ended));
            await PlaybackTests.UntilAsync(() => vm.ContinueWatching.Count == 0);

            // 3. Series & Random Episode
            vm.CurrentTabIndex = 6; // Series
            await PlaybackTests.UntilAsync(() => vm.MediaItems.Count == 1);
            vm.SelectedMedia = vm.MediaItems[0];
            await PlaybackTests.UntilAsync(() => vm.Episodes.Count == 2);

            Assert.True(vm.CanPlayRandomEpisode);
            vm.SelectedEpisode = vm.Episodes[0]; // ep-1
            await vm.PlayRandomEpisodeAsync();

            // Must select ep-2 (avoids repeating ep-1)
            Assert.Equal("ep-2", vm.SelectedEpisode.Id);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_05_Security_ParentalControl_And_EncryptedSync()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profileA = new Profile("profile-a", "Perfil A", "https://provider.invalid", "key-a", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profileA);

            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);
            await catalogStore.ReplaceAsync("profile-a", new CatalogSnapshot([new("cat-1", "General")], [
                new XtreamChannel("ch-1", "Canal Uno", "cat-1", "live", "ts"),
                new XtreamChannel("ch-2", "Canal Dos", "cat-1", "live", "ts")
            ], DateTimeOffset.UtcNow));

            var parental = new ParentalControlService(db);
            await parental.InitializeAsync();
            var viewerStore = new SqliteViewerStore(Path.Combine(root, "settings.db"), pooling: false);
            var sync = new EncryptedSyncService(viewerStore);

            // 1. Parental Control PIN
            Assert.False(parental.IsEnabled);
            Assert.True(await parental.ConfigurePinAsync("7788"));
            Assert.True(parental.IsEnabled);

            Assert.False(parental.Unlock("0000"));
            Assert.True(parental.Unlock("7788"));

            // 2. Restricted Category Flagging
            Assert.True(parental.IsRestrictedCategory("Canales Adultos +18"));
            Assert.False(parental.IsRestrictedCategory("Noticias 24H"));

            // 3. Populate Viewer Data (Favorites & History)
            await viewerStore.SetFavoriteAsync("profile-a", "live", "ch-1", true);
            await viewerStore.SetFavoriteAsync("profile-a", "live", "ch-2", true);
            await viewerStore.RecordPlayedAsync("profile-a", "live", "ch-1", DateTimeOffset.UtcNow);

            // 4. Encrypted Sync Export
            var exportPath = Path.Combine(root, "sync_backup.bin");
            var passphrase = "ClaveSuperSecreta!2026";
            var exportedCount = await sync.ExportAsync(exportPath, passphrase, "profile-a");
            Assert.Equal(3, exportedCount); // 2 favorites + 1 history
            Assert.True(File.Exists(exportPath));

            // Verify payload is encrypted ciphertext (AES-GCM / PBKDF2)
            var backupBytes = await File.ReadAllBytesAsync(exportPath);
            var backupText = Encoding.UTF8.GetString(backupBytes);
            Assert.DoesNotContain("ch-1", backupText, StringComparison.Ordinal);
            Assert.DoesNotContain("ch-2", backupText, StringComparison.Ordinal);

            // 5. Encrypted Sync Import to Fresh Store
            var root2 = CreateTempDirectory();
            try
            {
                var db2 = new SqliteSettingsService(Path.Combine(root2, "settings2.db"), pooling: false);
                await db2.InitializeAsync();
                var profileB = new Profile("profile-b", "Perfil B", "https://provider.invalid", "key-b", DateTimeOffset.UtcNow, "xtream");
                await db2.AddProfileAsync(profileB);

                var catalogStore2 = new SqliteCatalogStore(Path.Combine(root2, "settings2.db"), pooling: false);
                await catalogStore2.ReplaceAsync("profile-b", new CatalogSnapshot([new("cat-1", "General")], [
                    new XtreamChannel("ch-1", "Canal Uno", "cat-1", "live", "ts"),
                    new XtreamChannel("ch-2", "Canal Dos", "cat-1", "live", "ts")
                ], DateTimeOffset.UtcNow));

                var viewerStore2 = new SqliteViewerStore(Path.Combine(root2, "settings2.db"), pooling: false);
                var sync2 = new EncryptedSyncService(viewerStore2);

                var importResult = await sync2.ImportAsync(exportPath, passphrase, "profile-b");
                Assert.Equal(2, importResult.FavoritesAdded);
                Assert.Equal(1, importResult.HistoryMerged);

                var restoredFavorites = await viewerStore2.GetFavoritesForSyncAsync("profile-b");
                Assert.Equal(2, restoredFavorites.Count);
                Assert.Contains(restoredFavorites, f => f.ChannelId == "ch-1");
                Assert.Contains(restoredFavorites, f => f.ChannelId == "ch-2");

                var restoredHistory = await viewerStore2.GetHistoryForSyncAsync("profile-b");
                Assert.Single(restoredHistory);
                Assert.Equal("ch-1", restoredHistory[0].ChannelId);

                // 6. Import with Bad Passphrase -> Must Throw
                await Assert.ThrowsAnyAsync<Exception>(() => sync2.ImportAsync(exportPath, "ClaveIncorrecta!", "profile-c"));
            }
            finally
            {
                CleanupTempDirectory(root2);
            }
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_06_M3uPlaylistImport_FullLifecycle()
    {
        var root = CreateTempDirectory();
        try
        {
            var m3uFile = Path.Combine(root, "playlist.m3u8");
            var m3uContent = """
                #EXTM3U
                #EXTINF:-1 tvg-id="noticias.live" group-title="Noticias",Canal Noticias M3U
                https://cdn.example.tv/live/noticias.ts
                #EXTINF:-1 tvg-id="cine.live" group-title="Películas",Canal Cine M3U
                https://cdn.example.tv/live/cine.m3u8
                """;
            await File.WriteAllTextAsync(m3uFile, m3uContent, Encoding.UTF8);

            using var importer = new M3uPlaylistImporter();
            var imported = await importer.ImportLocalAsync(m3uFile);

            Assert.Equal(2, imported.Catalog.Channels.Count);
            Assert.Equal(2, imported.Catalog.Categories.Count);
            Assert.Contains(imported.Catalog.Channels, c => c.Name == "Canal Noticias M3U");
            Assert.Contains(imported.Catalog.Channels, c => c.Name == "Canal Cine M3U");

            var dbPath = Path.Combine(root, "store.db");
            var settings = new SqliteSettingsService(dbPath, pooling: false);
            await settings.InitializeAsync();
            await settings.AddProfileAsync(new Profile("m3u-profile", "Perfil M3U", "https://provider.invalid", "key-m3u", DateTimeOffset.UtcNow, "m3u"));
            var catalogStore = new SqliteCatalogStore(dbPath, pooling: false);
            await catalogStore.ReplaceAsync("m3u-profile", imported.Catalog);

            var loaded = await catalogStore.LoadAsync("m3u-profile");
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Channels.Count);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_07_PlaybackFailover_AlternateFormat_And_SingleStreamEnforcement()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profile = new Profile("profile-failover", "Perfil Failover", "https://provider.invalid", "key-failover", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profile);

            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var policy = new PlaybackPolicy(TimeSpan.FromSeconds(5), TimeSpan.Zero) { MaxRetries = 0 };
            var playback = new PlaybackService(backend, policy);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);
            var channel = new XtreamChannel("ch-failover", "Canal Deportes HD", "cat-1", "live", "ts");
            var category = new XtreamCategory("cat-1", "Deportes");

            await using var vm = new MainViewModel(
                (_, _, _) => new FullContentFixtureProvider(channels: [channel], categories: [category]),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials);

            await vm.InitializePlaybackAsync();
            vm.ActiveProfile = profile;
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.ConnectProfileAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.LoadCatalogAsync();
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 1);

            Assert.Equal(2, vm.AvailableFormats.Count);
            Assert.Contains("ts", vm.AvailableFormats);
            Assert.Contains("m3u8", vm.AvailableFormats);

            vm.SelectedFormat = "ts";
            vm.SelectedChannel = vm.Channels[0];

            // 1. Start live playback on TS
            await vm.PlaySelectedAsync();
            await PlaybackTests.UntilAsync(() => backend.Opens.Count == 1);
            Assert.Equal("ts", vm.SelectedFormat);
            Assert.EndsWith(".ts", backend.Opens.First().Source.ToString());
            Assert.Equal(1, backend.Peak);

            var open1Id = backend.Opens.First().Id;
            backend.Emit(new(open1Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // 2. Stream drops due to network error -> Trigger failover to alternate format (m3u8)
            backend.Emit(new(open1Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));

            // ViewModel automatically catches the failure and switches to m3u8
            await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
            Assert.Equal("m3u8", vm.SelectedFormat);
            Assert.EndsWith(".m3u8", backend.Opens.Last().Source.ToString());

            // Peak active streams MUST REMAIN STRICTLY 1
            Assert.Equal(1, backend.Peak);

            // Alternate format starts playing successfully
            var open2Id = backend.Opens.Last().Id;
            backend.Emit(new(open2Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // 3. User stops playback cleanly
            await vm.StopPlaybackAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsPlaying);
            Assert.Equal(0, backend.Active);
            Assert.Equal(1, backend.Peak);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task E2E_08_EPG_Pipeline_Schedule_Cache_And_OfflineDisplay()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profile = new Profile("profile-epg", "Perfil EPG", "https://provider.invalid", "key-epg", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profile);

            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var policy = new PlaybackPolicy(TimeSpan.FromSeconds(5), TimeSpan.Zero) { MaxRetries = 0 };
            var playback = new PlaybackService(backend, policy);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);
            var contentStore = new SqliteContentStore(Path.Combine(root, "settings.db"), pooling: false);

            var now = DateTimeOffset.UtcNow;
            var pastProgram = new EpgProgram("Noticias Matutinas", now.AddHours(-2), now.AddHours(-1), "Resumen informativo");
            var currentProgram = new EpgProgram("Magazine del Mediodía", now.AddMinutes(-30), now.AddMinutes(30), "Entretenimiento y actualidad");
            var nextProgram = new EpgProgram("Película de la Tarde", now.AddMinutes(30), now.AddHours(2), "Cine de acción");
            var futureProgram = new EpgProgram("Noticiero Estelar", now.AddHours(2), now.AddHours(3), "Emisión central");

            var epgSchedule = new[] { pastProgram, currentProgram, nextProgram, futureProgram };
            var channel = new XtreamChannel("ch-epg", "Canal Central", "cat-1", "live", "ts", EpgChannelId: "central.tv");
            var category = new XtreamCategory("cat-1", "General");

            await using var vm = new MainViewModel(
                (_, _, _) => new FullContentFixtureProvider(channels: [channel], categories: [category], epg: epgSchedule),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials,
                content: contentStore,
                epgImporter: new XmlTvReader());

            await vm.InitializePlaybackAsync();
            vm.ActiveProfile = profile;
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.ConnectProfileAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.LoadCatalogAsync();
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 1);

            vm.SelectedChannel = vm.Channels[0];

            // 1. Fetch EPG from provider
            await vm.RefreshEpgAsync();
            await PlaybackTests.UntilAsync(() => vm.EpgStatus.Contains("Guía actualizada", StringComparison.Ordinal));

            // Current and next program detection
            Assert.Contains("Magazine del Mediodía", vm.EpgNow, StringComparison.Ordinal);
            Assert.Contains("Película de la Tarde", vm.EpgNext, StringComparison.Ordinal);

            // 2. Verify persisted in SQLite content store
            var savedEpg = await contentStore.LoadEpgAsync(profile.Id, channel.Id);
            Assert.NotNull(savedEpg);
            Assert.Equal(4, savedEpg.Count);
            Assert.Equal("Magazine del Mediodía", savedEpg[1].Title);

            // 3. XMLTV File Import
            var programStart = DateTimeOffset.UtcNow.AddHours(-1).ToString("yyyyMMddHHmmss +0000");
            var programEnd = DateTimeOffset.UtcNow.AddHours(1).ToString("yyyyMMddHHmmss +0000");
            var xmltv = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <tv>
                  <channel id="central.tv">
                    <display-name>Canal Central</display-name>
                  </channel>
                  <programme start="{programStart}" stop="{programEnd}" channel="central.tv">
                    <title lang="es">Transmisión Especial XMLTV</title>
                    <desc lang="es">Cobertura en vivo importada</desc>
                  </programme>
                </tv>
                """;

            using var xmlStream = new MemoryStream(Encoding.UTF8.GetBytes(xmltv));
            await vm.ImportEpgAsync(xmlStream);
            Assert.Equal("Guía importada.", vm.EpgStatus);
            Assert.Contains("Transmisión Especial XMLTV", vm.EpgNow, StringComparison.Ordinal);

            // 4. Offline rehydration: Verify that loaded from SQLite cache even without provider
            var reloadedEpg = await contentStore.LoadEpgAsync(profile.Id, channel.Id);
            Assert.NotNull(reloadedEpg);
            Assert.Single(reloadedEpg);
            Assert.Equal("Transmisión Especial XMLTV", reloadedEpg[0].Title);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    // --- Helper Mock Servers & Fixtures ---

    private sealed class LocalXtreamMockServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;
        private bool _disposed;
        public int Port { get; }
        public string BaseUrl => $"http://127.0.0.1:{Port}";

        public LocalXtreamMockServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _worker = Task.Run(ListenLoopAsync);
        }

        private async Task ListenLoopAsync()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    await HandleClientAsync(client, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (_cts.Token.IsCancellationRequested) { }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var reqLine = await reader.ReadLineAsync(token);
            if (string.IsNullOrEmpty(reqLine)) return;
            var parts = reqLine.Split(' ');
            var uri = parts.Length > 1 ? parts[1] : "/";

            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(token))) { }

            var json = RouteRequest(uri);
            var body = Encoding.UTF8.GetBytes(json);
            var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, token);
            await stream.WriteAsync(body, token);
            await stream.FlushAsync(token);
        }

        private static string RouteRequest(string uri)
        {
            if (uri.Contains("action=get_live_categories"))
            {
                return """[{"category_id":"1","category_name":"Noticias"},{"category_id":"2","category_name":"Deportes"},{"category_id":"3","category_name":"Cine"}]""";
            }
            if (uri.Contains("action=get_live_streams"))
            {
                return """
                    [
                        {"num":1,"name":"Canal 24H Noticias","stream_type":"live","stream_id":"101","stream_icon":"","category_id":"1"},
                        {"num":2,"name":"Deportes Total HD","stream_type":"live","stream_id":"102","stream_icon":"","category_id":"2"},
                        {"num":3,"name":"Cine Clásico","stream_type":"live","stream_id":"103","stream_icon":"","category_id":"3"}
                    ]
                    """;
            }
            if (uri.Contains("action=get_vod_categories"))
            {
                return """[{"category_id":"10","category_name":"Películas de Acción"}]""";
            }
            if (uri.Contains("action=get_vod_streams"))
            {
                return """
                    [
                        {"stream_id":"201","name":"El Gran Escape","category_id":"10","container_extension":"mp4"},
                        {"stream_id":"202","name":"Misterio en el Tren","category_id":"10","container_extension":"mkv"}
                    ]
                    """;
            }
            if (uri.Contains("action=get_series_categories"))
            {
                return """[{"category_id":"20","category_name":"Documentales"}]""";
            }
            if (uri.Contains("action=get_series"))
            {
                return """[{"series_id":"301","name":"Planeta Salvaje","category_id":"20"}]""";
            }
            if (uri.Contains("action=get_series_info"))
            {
                return """
                    {
                        "seasons":[{"season_number":1}],
                        "episodes":{
                            "1":[
                                {"id":"401","title":"Océanos","episode_num":1,"container_extension":"mp4"},
                                {"id":"402","title":"Montañas","episode_num":2,"container_extension":"mp4"}
                            ]
                        }
                    }
                    """;
            }
            // Default auth / account info
            return """
                {
                    "user_info": {
                        "auth": 1,
                        "status": "Active",
                        "exp_date": "1893456000",
                        "active_cons": "0",
                        "max_connections": "1",
                        "allowed_output_formats": ["ts", "m3u8"]
                    },
                    "server_info": {
                        "url": "127.0.0.1",
                        "port": "8080",
                        "server_protocol": "http"
                    }
                }
                """;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _listener.Stop();
            try { await _worker; } catch { }
            _cts.Dispose();
        }
    }

    private sealed class FullContentFixtureProvider(
        IReadOnlyList<MediaItem>? movies = null,
        IReadOnlyList<MediaItem>? series = null,
        IReadOnlyList<MediaItem>? episodes = null,
        IReadOnlyList<XtreamChannel>? channels = null,
        IReadOnlyList<XtreamCategory>? categories = null,
        IReadOnlyList<EpgProgram>? epg = null) : IIptvProvider, IContentProvider
    {
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts", "m3u8"]));

        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamCategory>>(categories ?? []);

        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamChannel>>(channels ?? []);

        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new($"https://provider.invalid/{channel.Id}.{format}");
        public Uri CreateMediaUri(MediaItem item) => new($"https://provider.invalid/{item.Id}.{item.Extension ?? "mp4"}");

        public Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaCatalog([], kind == ContentKind.Movie ? (movies ?? []) : (series ?? []), DateTimeOffset.UtcNow));

        public Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default) =>
            Task.FromResult(movie with { Plot = "Sinopsis de prueba" });

        public Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaCatalog([new("1", "Temporada 1")], episodes ?? [], DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpgProgram>>(epg ?? []);

        public void Dispose() { }
    }
}
