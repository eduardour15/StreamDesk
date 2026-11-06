using System.IO;
using System.Net;
using System.Security.Cryptography;
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
public sealed class ChaoticUserResilienceTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "streamdesk-chaos-" + Guid.NewGuid().ToString("N"));
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
    public async Task ChaoticUser_01_TypesGibberishAndMaliciousInputs_HandledDefensively()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var credentials = new FixtureCredentials();
            var profileService = new ProfileService(db, credentials);
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);

            await using var vm = new MainViewModel(
                (_, _, _) => new FixtureProvider(CatalogFixtures.Create(2)),
                playback,
                profileService,
                logger,
                new CatalogService(catalogStore),
                credentials);

            // 1. Empty or whitespace profile name
            vm.ProfileBaseUrl = "https://valid-provider.tv:8080";
            vm.ProfileUsername = "valid-user";
            vm.ProfileName = "   ";
            var createdWhitespaceName = await vm.CreateProfileAsync("password123");
            Assert.False(createdWhitespaceName);
            Assert.Contains("El nombre debe tener entre 1 y 80 caracteres", vm.ProfileStatus, StringComparison.OrdinalIgnoreCase);

            // 2. Huge name (> 80 chars)
            vm.ProfileName = new string('A', 150);
            var createdHugeName = await vm.CreateProfileAsync("password123");
            Assert.False(createdHugeName);
            Assert.Contains("El nombre debe tener entre 1 y 80 caracteres", vm.ProfileStatus, StringComparison.OrdinalIgnoreCase);

            // 3. Empty username
            vm.ProfileName = "Mi Perfil";
            vm.ProfileUsername = "   ";
            var createdEmptyUser = await vm.CreateProfileAsync("password123");
            Assert.False(createdEmptyUser);
            Assert.Contains("Revisa el usuario y la contraseña", vm.ProfileStatus, StringComparison.OrdinalIgnoreCase);

            // 4. Empty password
            vm.ProfileUsername = "admin";
            var createdEmptyPass = await vm.CreateProfileAsync("");
            Assert.False(createdEmptyPass);
            Assert.Contains("Revisa el usuario y la contraseña", vm.ProfileStatus, StringComparison.OrdinalIgnoreCase);

            // 5. Malformed, Non-HTTP or Embedded-Credential URLs
            var badUrls = new[]
            {
                "not an url at all",
                "ftp://server.tv:21",
                "file:///C:/Windows/System32",
                "http://user:pass@provider.tv:8080",
                "http://provider.tv:8080/get.php?user=1&pass=2",
                "http://provider.tv:8080/live/user/pass/1.ts",
                "http://::::8080"
            };

            foreach (var badUrl in badUrls)
            {
                vm.ProfileBaseUrl = badUrl;
                var result = await vm.CreateProfileAsync("password123");
                Assert.False(result);
                Assert.Contains("Usa una URL base HTTP o HTTPS", vm.ProfileStatus, StringComparison.OrdinalIgnoreCase);
            }

            // Database must remain completely clean (0 profiles added)
            var profiles = await db.GetProfilesAsync();
            Assert.Empty(profiles);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task ChaoticUser_02_SpamsPlaybackControlsWithZeroMediaLoaded()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);

            await using var vm = new MainViewModel(
                (_, _, _) => new FixtureProvider(CatalogFixtures.Create(2)),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials);

            await vm.InitializePlaybackAsync();

            // Dumb user opens app with 0 channels and begins spamming playback controls
            await vm.PlaySelectedAsync();
            Assert.Equal("Haz clic en un canal para reproducir.", vm.StatusMessage);

            await vm.PlayMediaAsync();
            Assert.Equal("Elige una película o episodio.", vm.ContentStatus);

            await vm.TogglePlaybackAsync();
            await vm.StopPlaybackAsync();
            await vm.RetryPlaybackAsync();

            // Seeking with nothing loaded
            await vm.SeekToAsync(-500);
            await vm.SeekToAsync(999999);
            await vm.SeekRelativeAsync(-30);
            await vm.SeekRelativeAsync(30);

            // Audio & subtitle track selection with nothing loaded
            await vm.SelectTrackAsync(false);
            await vm.SelectTrackAsync(true);

            // Series & random episode commands with nothing loaded
            await vm.PlayRandomEpisodeAsync();
            await vm.NavigateEpisodeAsync(1);
            await vm.NavigateEpisodeAsync(-1);

            // Favorite toggle with no channel
            await vm.ToggleFavoriteAsync();

            // Refresh details
            await vm.RefreshPlaybackDetailsAsync();

            // Assert: zero streams were opened, backend never crashed or opened anything
            Assert.Empty(backend.Opens);
            Assert.Equal(0, backend.Active);
            Assert.Equal(0, backend.Peak);
            Assert.False(vm.IsPlaying);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task ChaoticUser_03_ImpatientCompulsiveZapper_Spams25ChannelsInRow_StrictlyOneStreamPeak()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profile = new Profile("profile-zap", "Perfil Zapper", "https://provider.invalid", "key-zap", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profile);

            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);

            var channels = Enumerable.Range(1, 10)
                .Select(i => new XtreamChannel($"ch-{i}", $"Canal {i}", "cat-1", "live", "ts"))
                .ToArray();

            await using var vm = new MainViewModel(
                (_, _, _) => new ChaoticFixtureProvider(channels),
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
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 10);

            // The user rapidly clicks from channel to channel 25 times without waiting
            for (int i = 0; i < 25; i++)
            {
                vm.SelectedChannel = vm.Channels[i % 10];
                _ = vm.PlaySelectedAsync(); // Deliberately unawaited rapid fire
            }

            // Now settle on the final channel and await it properly
            var finalChannel = vm.Channels[7];
            vm.SelectedChannel = finalChannel;
            await vm.PlaySelectedAsync();

            await PlaybackTests.UntilAsync(() => backend.Opens.Count > 0);
            var lastOpenId = backend.Opens.Last().Id;
            backend.Emit(new(lastOpenId, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // Invariant: Despite 25+ rapid zaps, active streams must NEVER exceed 1
            Assert.Equal(1, backend.Peak);
            Assert.Equal(1, backend.Active);
            Assert.Equal(finalChannel.Id, vm.PlayingChannelId);

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
    public async Task ChaoticUser_04_RenamesExecutablesAndRandomJunkToM3uAndXml_RejectsSafely()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);
            using var importer = new M3uPlaylistImporter();

            await using var vm = new MainViewModel(
                (_, _, _) => new FixtureProvider(CatalogFixtures.Create(2)),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials,
                playlistImporter: importer,
                epgImporter: new XmlTvReader());

            // 1. User imports a 0-byte file renamed to playlist.m3u
            var zeroByteFile = Path.Combine(root, "empty.m3u");
            await File.WriteAllBytesAsync(zeroByteFile, []);
            await vm.ImportLocalPlaylistAsync(zeroByteFile);
            Assert.Contains("cabecera M3U válida", vm.PlaylistStatus, StringComparison.OrdinalIgnoreCase);
            Assert.False(vm.HasImportedPlaylist);

            // 2. User imports a random binary file (e.g. simulated JPEG / EXE) renamed to list.m3u8
            var junkFile = Path.Combine(root, "junk.m3u8");
            var randomBytes = new byte[65536];
            Random.Shared.NextBytes(randomBytes);
            await File.WriteAllBytesAsync(junkFile, randomBytes);
            await vm.ImportLocalPlaylistAsync(junkFile);
            Assert.Contains("cabecera M3U válida", vm.PlaylistStatus, StringComparison.OrdinalIgnoreCase);
            Assert.False(vm.HasImportedPlaylist);

            // 2b. User imports file with header but without valid channels
            var invalidChannelsFile = Path.Combine(root, "no_channels.m3u");
            await File.WriteAllTextAsync(invalidChannelsFile, "#EXTM3U\n#EXTINF:-1,Invalido\nftp://invalid-stream\n");
            await vm.ImportLocalPlaylistAsync(invalidChannelsFile);
            Assert.Contains("no contiene canales", vm.PlaylistStatus, StringComparison.OrdinalIgnoreCase);
            Assert.False(vm.HasImportedPlaylist);

            // 3. User tries importing a non-m3u extension (e.g. video.mp4)
            var mp4File = Path.Combine(root, "video.mp4");
            await File.WriteAllTextAsync(mp4File, "#EXTM3U\nhttps://cdn.example.tv/live.ts");
            await vm.ImportLocalPlaylistAsync(mp4File);
            Assert.Equal("Selecciona un archivo .m3u o .m3u8.", vm.PlaylistStatus);

            // 4. User inputs invalid or local SSRF addresses in remote playlist
            await vm.ImportRemotePlaylistAsync("not an url");
            Assert.Equal("Escribe una dirección HTTP o HTTPS válida.", vm.PlaylistStatus);

            // 5. User imports random corrupt XML as EPG
            vm.SelectedChannel = new XtreamChannel("test-ch", "Canal Test", "cat-1", "live", "ts", EpgChannelId: "epg.1");
            using var corruptXml = new MemoryStream(Encoding.UTF8.GetBytes("<tv><broken-unclosed-tag><random-data>"));
            await vm.ImportEpgAsync(corruptXml);
            Assert.Equal("El archivo de guía no es válido.", vm.EpgStatus);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task ChaoticUser_05_CorruptsBackupFileBits_AesGcmAuthenticationDetectsTampering()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteViewerStore(Path.Combine(root, "viewer.db"), pooling: false);
            var settings = new SqliteSettingsService(Path.Combine(root, "viewer.db"), pooling: false);
            await settings.InitializeAsync();
            var profile = new Profile("profile-sync", "Perfil Sync", "https://provider.invalid", "key-sync", DateTimeOffset.UtcNow, "xtream");
            await settings.AddProfileAsync(profile);

            // Add channel to SQLite so foreign keys are satisfied
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "viewer.db"), pooling: false);
            await catalogStore.ReplaceAsync("profile-sync", new CatalogSnapshot([new("cat-1", "General")], [
                new XtreamChannel("ch-1", "Canal Uno", "cat-1", "live", "ts")
            ], DateTimeOffset.UtcNow));

            await db.SetFavoriteAsync("profile-sync", "live", "ch-1", true);
            await db.RecordPlayedAsync("profile-sync", "live", "ch-1", DateTimeOffset.UtcNow);

            var sync = new EncryptedSyncService(db);
            var backupFile = Path.Combine(root, "backup.streamdesk");
            var passphrase = "Password123!";

            // Legitimate export
            await sync.ExportAsync(backupFile, passphrase, "profile-sync");
            Assert.True(File.Exists(backupFile));

            // User or corrupted drive flips 1 byte inside ciphertext
            var fileBytes = await File.ReadAllBytesAsync(backupFile);
            fileBytes[^10] ^= 0xAA; // Flip bits
            var corruptedFile = Path.Combine(root, "corrupted.streamdesk");
            await File.WriteAllBytesAsync(corruptedFile, fileBytes);

            // Importing tampered backup must throw CryptographicException
            var ex = await Assert.ThrowsAsync<CryptographicException>(() => sync.ImportAsync(corruptedFile, passphrase, "profile-sync"));
            Assert.Equal("La contraseña es incorrecta o los datos fueron alterados.", ex.Message);

            // Empty or truncated file
            var truncatedFile = Path.Combine(root, "truncated.streamdesk");
            await File.WriteAllBytesAsync(truncatedFile, [0x01, 0x02, 0x03]);
            await Assert.ThrowsAsync<InvalidDataException>(() => sync.ImportAsync(truncatedFile, passphrase, "profile-sync"));
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task ChaoticUser_06_ParentalControl_CluelessPinGuessesAndFormattingAbuse()
    {
        var root = CreateTempDirectory();
        try
        {
            var settings = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await settings.InitializeAsync();
            var parental = new ParentalControlService(settings);
            await parental.InitializeAsync();

            // 1. Clueless user tries configuring PIN with invalid formats
            var badPins = new[] { "", "   ", "12", "123", "123456789", "abcd", "12a4", "12 4", "12-4" };
            foreach (var badPin in badPins)
            {
                await Assert.ThrowsAsync<ArgumentException>(() => parental.ConfigurePinAsync(badPin));
            }

            // 2. Configure legitimate PIN "4321"
            await parental.ConfigurePinAsync("4321");
            Assert.True(parental.IsEnabled);
            Assert.True(parental.IsLocked);

            // 3. Clueless guesses to unlock
            Assert.False(parental.Unlock(""));
            Assert.False(parental.Unlock("0000"));
            Assert.False(parental.Unlock("1234"));
            Assert.False(parental.Unlock("432"));
            Assert.False(parental.Unlock("43210"));
            Assert.False(parental.Unlock("ABCD"));
            Assert.True(parental.IsLocked);

            // Restricted categories remain restricted
            Assert.True(parental.IsRestrictedCategory("Canales Adultos 18+"));
            Assert.True(parental.IsRestrictedCategory("XXX Movies"));
            Assert.False(parental.IsRestrictedCategory("Deportes y Noticias"));

            // 4. Correct PIN unlocks
            Assert.True(parental.Unlock("4321"));
            Assert.False(parental.IsLocked);

            // 5. Try disabling with wrong PIN
            Assert.False(await parental.DisableAsync("0000"));
            Assert.True(parental.IsEnabled);

            // Disable with correct PIN
            Assert.True(await parental.DisableAsync("4321"));
            Assert.False(parental.IsEnabled);
            Assert.False(parental.IsLocked);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public async Task ChaoticUser_07_DestroysActiveProfileWhileLiveStreamIsPlaying()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profileDoom = new Profile("profile-doom", "Perfil Condenado", "https://provider.invalid", "key-doom", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profileDoom);

            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);
            var channel = new XtreamChannel("ch-doom", "Canal Doom HD", "cat-1", "live", "ts");

            await using var vm = new MainViewModel(
                (_, _, _) => new ChaoticFixtureProvider([channel]),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials,
                settings: db);

            await vm.InitializePlaybackAsync();
            vm.ReloadProfilesCommand.Execute(null);
            await PlaybackTests.UntilAsync(() => vm.Profiles.Count == 1);
            vm.ActiveProfile = vm.Profiles[0];
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.ConnectProfileAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking);
            await vm.LoadCatalogAsync();
            await PlaybackTests.UntilAsync(() => vm.Channels.Count == 1);

            // 1. Play channel
            vm.SelectedChannel = vm.Channels[0];
            await vm.PlaySelectedAsync();
            await PlaybackTests.UntilAsync(() => backend.Opens.Count == 1);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);
            Assert.Equal(1, backend.Active);

            // 2. While video is actively playing, user goes to Settings and deletes this active profile!
            vm.SelectedProfile = vm.Profiles.FirstOrDefault(p => p.Id == profileDoom.Id);
            Assert.NotNull(vm.SelectedProfile);
            vm.DeleteProfileCommand.Execute(null);
            await PlaybackTests.UntilAsync(() => vm.Profiles.Count == 0);

            // 3. Profile was deleted from SQLite
            var remainingProfiles = await db.GetProfilesAsync();
            Assert.Empty(remainingProfiles);
            Assert.Equal("Cuenta eliminada de este equipo.", vm.ProfileStatus);
            Assert.Null(vm.SelectedProfile);

            // 4. Playback can be stopped cleanly without deadlock or DB errors
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
    public async Task ChaoticUser_08_PoundingVolumeSliderAndBoosterCycleInsanely()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);

            await using var vm = new MainViewModel(
                (_, _, _) => new FixtureProvider(CatalogFixtures.Create(2)),
                playback,
                new ProfileService(db, credentials),
                logger,
                new CatalogService(catalogStore),
                credentials);

            // 1. Drag volume to negative extreme
            vm.Volume = -99999;
            Assert.Equal(0, vm.Volume);
            Assert.Equal("0%", vm.VolumeLabel);

            // 2. Drag volume to positive extreme
            vm.Volume = 999999;
            Assert.Equal(PlaybackService.MaxVolume, vm.Volume); // 400
            Assert.Equal("400%", vm.VolumeLabel);

            // 3. Booster cycle spam 50 times in a row
            for (int i = 0; i < 50; i++)
            {
                vm.CycleAmplification();
                Assert.InRange(vm.AmplificationDb, 0, PlaybackService.MaxAmplificationDb);
                Assert.InRange(vm.Volume, 0, PlaybackService.MaxVolume);
            }

            // Normal volume 80%
            vm.Volume = 80;
            Assert.Equal(80, vm.Volume);
            Assert.Equal("80%", vm.VolumeLabel);
            Assert.False(vm.IsAmplified);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    [Fact]
    public void ChaoticUser_09_CrazySearchQueries_CatastrophicReDosAndSpecialChars()
    {
        var channels = new List<XtreamChannel>
        {
            new("1", "Canal Uno Noticias HD", "cat-1", "live", "ts"),
            new("2", "Películas de Acción 4K", "cat-2", "live", "ts"),
            new("3", "Deportes Fútbol en Vivo", "cat-3", "live", "ts")
        };

        var search = new CatalogSearch(channels);

        // 1. Huge query (5,000 characters)
        var hugeQuery = new string('x', 5000);
        var res1 = search.Filter(hugeQuery, null, false, null);
        Assert.Empty(res1);

        // 2. ReDoS triggers and unbalanced brackets/parens
        var crazyQueries = new[]
        {
            "((((((((a+)+)+)+)+)+)+)+)",
            ".*.*.*.*.*.*.*.*.*.*.*",
            "[[[[[[[[[[[[[[[[[[[[",
            "????????????????????",
            "\\p{C}\\p{M}\\p{Z}",
            "'\"; DROP TABLE Channels; --",
            "<script>alert('xss')</script>",
            "🍿🔥📺✨🎮",
            "Canal\0\t\r\nHD"
        };

        foreach (var query in crazyQueries)
        {
            // Must execute instantaneously and never throw regex/syntax exceptions
            var results = search.Filter(query, null, false, null);
            Assert.NotNull(results);
        }

        // 3. Normal accents & case insensitivity
        var resAccion = search.Filter("accion", null, false, null);
        Assert.Single(resAccion);
        Assert.Equal("2", resAccion[0].Id);

        var resFutbol = search.Filter("FÚTBOL", null, false, null);
        Assert.Single(resFutbol);
        Assert.Equal("3", resFutbol[0].Id);
    }

    [Fact]
    public async Task ChaoticUser_10_RapidFirePlayPauseToggleSpam()
    {
        var root = CreateTempDirectory();
        try
        {
            var db = new SqliteSettingsService(Path.Combine(root, "settings.db"), pooling: false);
            await db.InitializeAsync();
            var profile = new Profile("profile-pause", "Perfil Pausa", "https://provider.invalid", "key-pause", DateTimeOffset.UtcNow, "xtream");
            await db.AddProfileAsync(profile);

            var credentials = new FixtureCredentials();
            var backend = new ControlledBackend();
            var playback = new PlaybackService(backend);
            var logger = new FixtureLogger();
            var catalogStore = new SqliteCatalogStore(Path.Combine(root, "settings.db"), pooling: false);
            var channel = new XtreamChannel("ch-1", "Canal Prueba", "cat-1", "live", "ts");

            await using var vm = new MainViewModel(
                (_, _, _) => new ChaoticFixtureProvider([channel]),
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

            vm.SelectedChannel = vm.Channels[0];
            await vm.PlaySelectedAsync();
            await PlaybackTests.UntilAsync(() => backend.Opens.Count == 1);
            var openId = backend.Opens.Last().Id;
            backend.Emit(new(openId, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => vm.IsPlaying);

            // User mashes TogglePlaybackAsync (Play/Pause button or Spacebar) 20 times in a row
            for (int i = 0; i < 20; i++)
            {
                await vm.TogglePlaybackAsync();
                if (vm.IsPaused)
                {
                    backend.Emit(new(openId, PlaybackSignalKind.Paused));
                }
                else
                {
                    backend.Emit(new(openId, PlaybackSignalKind.Playing));
                }
            }

            // Invariant: Streams remain strictly 1
            Assert.Equal(1, backend.Peak);

            await vm.StopPlaybackAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsPlaying);
            Assert.Equal(0, backend.Active);
        }
        finally
        {
            CleanupTempDirectory(root);
        }
    }

    private sealed class ChaoticFixtureProvider(IReadOnlyList<XtreamChannel> channels) : IIptvProvider
    {
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts", "m3u8"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamCategory>>([new("cat-1", "General")]);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new($"https://provider.invalid/{channel.Id}.{format}");
        public void Dispose() { }
    }
}
