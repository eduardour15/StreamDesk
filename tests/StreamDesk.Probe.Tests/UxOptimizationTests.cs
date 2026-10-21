using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using Xunit;

namespace StreamDesk.Probe.Tests;

public sealed class UxOptimizationTests
{
    [Fact]
    public async Task ChannelLogoLoader_SupportsContextualDecodingAndSafeQueryParameters()
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(
            300, 450, 96, 96, PixelFormats.Bgra32, null, new byte[300 * 450 * 4], 300 * 4)));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        var pngBytes = ms.ToArray();

        using var loader = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pngBytes)
            }))));

        Assert.Equal(500, ChannelLogoLoader.MaxCacheEntries);

        // Contextual decode for TV logo (64x64) vs Movie Poster (240x360) with valid CDN query string
        var tvLogo = await loader.GetAsync("https://cdn.invalid/logo.png?size=small&v=2", 64, 64, CancellationToken.None);
        var moviePoster = await loader.GetAsync("https://image.tmdb.invalid/t/p/original/poster.png?w=240&h=360&sig=abc123", 240, 360, CancellationToken.None);

        Assert.NotNull(tvLogo);
        Assert.NotNull(moviePoster);
        Assert.Equal(64, tvLogo!.PixelWidth);
        Assert.Equal(240, moviePoster!.PixelWidth);
        Assert.Equal(2, loader.CachedCount);

        // Sensitive query parameters or UserInfo must still be rejected
        Assert.Null(await loader.GetAsync("https://cdn.invalid/logo.png?password=secret", 64, 64, CancellationToken.None));
        Assert.Null(await loader.GetAsync("https://cdn.invalid/logo.png?token=abc", 64, 64, CancellationToken.None));
        Assert.Null(await loader.GetAsync("https://cdn.invalid/logo.png?api_key=abc", 64, 64, CancellationToken.None));
        Assert.Null(await loader.GetAsync("https://user:pass@cdn.invalid/logo.png", 64, 64, CancellationToken.None));
    }

    [Fact]
    public void VerifyFluentSymbolsExist()
    {
        var symbols = new[]
        {
            Wpf.Ui.Controls.SymbolRegular.Search24,
            Wpf.Ui.Controls.SymbolRegular.Dismiss24,
            Wpf.Ui.Controls.SymbolRegular.Folder24,
            Wpf.Ui.Controls.SymbolRegular.MoviesAndTv24,
            Wpf.Ui.Controls.SymbolRegular.Tv24,
            Wpf.Ui.Controls.SymbolRegular.Headphones24,
            Wpf.Ui.Controls.SymbolRegular.ClosedCaption24,
            Wpf.Ui.Controls.SymbolRegular.DocumentBulletList24,
            Wpf.Ui.Controls.SymbolRegular.Settings24,
            Wpf.Ui.Controls.SymbolRegular.LockClosed24,
            Wpf.Ui.Controls.SymbolRegular.ArrowSync24,
            Wpf.Ui.Controls.SymbolRegular.Pin24,
            Wpf.Ui.Controls.SymbolRegular.FullScreenMaximize24,
            Wpf.Ui.Controls.SymbolRegular.Save24,
            Wpf.Ui.Controls.SymbolRegular.Delete24,
            Wpf.Ui.Controls.SymbolRegular.PlugDisconnected24,
            Wpf.Ui.Controls.SymbolRegular.Star24,
            Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24,
            Wpf.Ui.Controls.SymbolRegular.ChevronDown24,
            Wpf.Ui.Controls.SymbolRegular.ArrowLeft24,
            Wpf.Ui.Controls.SymbolRegular.Live24,
            Wpf.Ui.Controls.SymbolRegular.Play24,
            Wpf.Ui.Controls.SymbolRegular.Pause24,
            Wpf.Ui.Controls.SymbolRegular.Speaker224,
            Wpf.Ui.Controls.SymbolRegular.SpeakerMute24,
            Wpf.Ui.Controls.SymbolRegular.ArrowReset24,
            Wpf.Ui.Controls.SymbolRegular.ArrowRepeatAll24,
            Wpf.Ui.Controls.SymbolRegular.Stop24,
            Wpf.Ui.Controls.SymbolRegular.Previous24,
            Wpf.Ui.Controls.SymbolRegular.Next24,
            Wpf.Ui.Controls.SymbolRegular.Home24,
            Wpf.Ui.Controls.SymbolRegular.History24,
            Wpf.Ui.Controls.SymbolRegular.Globe24,
            Wpf.Ui.Controls.SymbolRegular.Color24,
            Wpf.Ui.Controls.SymbolRegular.Person24,
            Wpf.Ui.Controls.SymbolRegular.ArrowUpload24,
            Wpf.Ui.Controls.SymbolRegular.ArrowDownload24,
            Wpf.Ui.Controls.SymbolRegular.ChevronUp24,
            Wpf.Ui.Controls.SymbolRegular.ChevronRight24,
            Wpf.Ui.Controls.SymbolRegular.LockOpen24
        };
        Assert.Equal(40, symbols.Length);
    }

    [Fact]
    public void Converters_FormatActiveChannelEqualityAndEpgRowSubtitle()
    {
        var equality = new EqualityConverter();
        Assert.Equal(true, equality.Convert(new object[] { "ch-10", "ch-10" }, typeof(bool), string.Empty, CultureInfo.InvariantCulture));
        Assert.Equal(false, equality.Convert(new object[] { "ch-10", "ch-20" }, typeof(bool), string.Empty, CultureInfo.InvariantCulture));
        Assert.Equal(false, equality.Convert(new object[] { "ch-10", null! }, typeof(bool), string.Empty, CultureInfo.InvariantCulture));

        var epgConverter = new EpgSubtitleConverter();
        var programs = new Dictionary<string, IReadOnlyList<EpgProgram>>(StringComparer.OrdinalIgnoreCase)
        {
            ["epg.sports"] = [new EpgProgram("Final Champions", DateTimeOffset.UtcNow.AddMinutes(-15), DateTimeOffset.UtcNow.AddMinutes(45))]
        };

        var withEpgAndDial = (string)epgConverter.Convert(new object[] { "101", "epg.sports", programs }, typeof(string), string.Empty, CultureInfo.InvariantCulture);
        Assert.Contains("Dial 101", withEpgAndDial, StringComparison.Ordinal);
        Assert.Contains("Final Champions", withEpgAndDial, StringComparison.Ordinal);

        var withoutEpg = (string)epgConverter.Convert(new object[] { "canal-noticias", null!, programs }, typeof(string), string.Empty, CultureInfo.InvariantCulture);
        Assert.Equal("Emisión en directo", withoutEpg);
    }

    [Fact]
    public void PlaybackShortcuts_SupportRelativeSeekAndToggleKeys()
    {
        Assert.Equal(PlaybackShortcut.SeekBackward, PlaybackShortcuts.Resolve(Key.J, ModifierKeys.None, false, false, true));
        Assert.Equal(PlaybackShortcut.SeekForward, PlaybackShortcuts.Resolve(Key.L, ModifierKeys.None, false, false, true));
        Assert.Equal(PlaybackShortcut.Toggle, PlaybackShortcuts.Resolve(Key.K, ModifierKeys.None, false, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(Key.J, ModifierKeys.None, true, false, true));
    }

    [Fact]
    public async Task ViewModel_SupportsOsdHud_VolumeLabel_RelativeSeek_ActiveIndicators_And_EmptyState()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend
        {
            Details = new MediaPlaybackDetails(30_000, 120_000, true, [new(1, "Español")], [new(-1, "Desactivados")])
        };
        var playback = new PlaybackService(backend);
        await using var vm = new MainViewModel(
            (_, _, _) => new VodFixtureProvider(),
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.SqliteContentStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.XmlTvReader());

        await vm.LoadProfilesAsync();

        // UX-OPT-T04: Movies/Series tabs accessible & HasNoMediaItems true initially
        vm.CurrentTabIndex = 5;
        Assert.True(vm.ShowMovieBrowse);
        Assert.True(vm.HasNoMediaItems);

        // UX-OPT-T08: ConnectedProfileId tracks connected profile
        Assert.Null(vm.ConnectedProfileId);
        await vm.ConnectProfileAsync();
        Assert.Equal("first", vm.ConnectedProfileId);

        // UX-OPT-T05: PlayingChannelId tracks active channel
        vm.CurrentTabIndex = 1;
        await vm.LoadCatalogAsync();
        vm.SelectedChannel = vm.Channels[0];
        await vm.PlaySelectedAsync();
        Assert.Equal(vm.Channels[0].Id, vm.PlayingChannelId);

        // UX-OPT-T07: VolumeLabel & OSD HUD
        vm.Volume = 75;
        Assert.Equal("75%", vm.VolumeLabel);
        Assert.True(vm.ShowOsd);
        Assert.Contains("75%", vm.OsdText, StringComparison.Ordinal);

        vm.IsMuted = true;
        Assert.Contains("Silenciado", vm.OsdText, StringComparison.Ordinal);

        // UX-OPT-T04 & UX-OPT-T07: Load VOD movie and test relative seek (-10s / +10s)
        await vm.ProbeContentAsync();
        vm.CurrentTabIndex = 5;
        while (vm.MediaItems.Count == 0) await Task.Delay(10);
        Assert.False(vm.HasNoMediaItems);
        vm.SelectedMedia = vm.MediaItems[0];
        await vm.PlayMediaAsync();
        await vm.RefreshPlaybackDetailsAsync();
        Assert.True(vm.CanSeek);
        await vm.SeekRelativeAsync(10);
        Assert.Equal(40_000, backend.Sought);
        Assert.Contains("+10s", vm.OsdText, StringComparison.Ordinal);

        backend.Details = new MediaPlaybackDetails(40_000, 120_000, true, [new(1, "Español")], [new(-1, "Desactivados")]);
        await vm.RefreshPlaybackDetailsAsync();
        await vm.SeekRelativeAsync(-10);
        Assert.Equal(30_000, backend.Sought);
        Assert.Contains("-10s", vm.OsdText, StringComparison.Ordinal);

        await vm.StopPlaybackAsync();
        Assert.Null(vm.PlayingChannelId);
    }

    [Fact]
    public async Task ViewModel_EpisodePlayerFocus_And_ReturnToEpisodes_PreservesContext()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        await using var vm = new MainViewModel(
            (_, _, _) => new VodFixtureProvider(),
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.SqliteContentStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.XmlTvReader());

        await vm.LoadProfilesAsync();
        await vm.ProbeContentAsync();

        // Navigate to Series tab (6)
        vm.CurrentTabIndex = 6;
        while (vm.MediaItems.Count == 0) await Task.Delay(10);
        Assert.True(vm.ShowSeriesBrowse);

        // Select the series
        var series = vm.MediaItems[0];
        vm.SelectedMedia = series;
        while (vm.Episodes.Count == 0) await Task.Delay(10);

        Assert.True(vm.ShowSeriesDetails);
        Assert.False(vm.IsEpisodePlayerFocus);
        Assert.Equal(2, vm.Episodes.Count);

        // Select and play an episode with episode focus
        var chosenEpisode = vm.Episodes[0];
        vm.SelectedEpisode = chosenEpisode;
        vm.IsEpisodePlayerFocus = true;

        Assert.True(vm.IsEpisodePlayerFocus);
        Assert.Equal("Episodio 1", vm.NowPlaying);

        // Returning to episodes must reset focus mode while preserving series, season and selected episode
        vm.ReturnToEpisodes();
        Assert.False(vm.IsEpisodePlayerFocus);
        Assert.Same(series, vm.SelectedMedia);
        Assert.NotNull(vm.Season);
        Assert.Same(chosenEpisode, vm.SelectedEpisode);
        Assert.True(vm.ShowSeriesDetails);

        // Switching tab resets focus
        vm.IsEpisodePlayerFocus = true;
        vm.CurrentTabIndex = 1;
        Assert.False(vm.IsEpisodePlayerFocus);
    }

    [Fact]
    public async Task SearchableCategoryFilters_LiveAndMedia_FilterAndUnifySearchCorrectly()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        await using var vm = new MainViewModel(
            (_, _, _) => new VodFixtureProvider(),
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.SqliteContentStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.XmlTvReader());

        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();

        // 1. Live TV Category Search
        vm.CurrentTabIndex = 1;
        await vm.LoadCatalogAsync();
        var initialLiveCount = vm.VisibleCategories.Count;
        Assert.True(initialLiveCount > 1);

        // Searching non-matching query leaves only the default "Todas las categorías"
        vm.CategorySearch = "inexistente_xyz";
        Assert.True(vm.HasCategorySearch);
        Assert.Single(vm.VisibleCategories);
        Assert.Equal("Todas las categorías", vm.VisibleCategories[0].Name);

        // Clearing category search restores all categories
        vm.CategorySearch = "";
        Assert.False(vm.HasCategorySearch);
        Assert.Equal(initialLiveCount, vm.VisibleCategories.Count);

        // 2. Media (Movies/Series) Unified Category Search
        vm.CurrentTabIndex = 5;
        await vm.ProbeContentAsync();
        while (vm.MediaItems.Count == 0) await Task.Delay(10);

        var initialMediaCatCount = vm.VisibleMediaCategories.Count;
        Assert.True(initialMediaCatCount >= 2); // "Todas las categorías" + "Acción"

        vm.MediaCategorySearch = "Acción";
        Assert.True(vm.HasMediaCategorySearch);
        Assert.Equal(2, vm.VisibleMediaCategories.Count);
        Assert.Equal("Acción", vm.VisibleMediaCategories[1].Name);

        vm.MediaCategorySearch = "no_existe_categoria";
        Assert.Single(vm.VisibleMediaCategories);

        vm.MediaCategorySearch = "";
        Assert.False(vm.HasMediaCategorySearch);
        Assert.Equal(initialMediaCatCount, vm.VisibleMediaCategories.Count);
    }

    [Fact]
    public async Task ImmersiveMode_WorksAcrossLiveTvMoviesAndSeries_PreservingContextAndStream()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        await using var vm = new MainViewModel(
            (_, _, _) => new VodFixtureProvider(),
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.SqliteContentStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.XmlTvReader());

        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();
        await vm.LoadCatalogAsync();
        await vm.ProbeContentAsync();

        // 1. Live TV Immersive Mode
        vm.CurrentTabIndex = 1;
        vm.SearchText = "Canal";
        await PlaybackTests.UntilAsync(() => vm.Channels.Count > 0);
        var liveChannel = vm.Channels[0];
        vm.SelectedChannel = liveChannel;
        await vm.PlaySelectedAsync();
        Assert.True(vm.IsImmersiveMode);
        Assert.Equal("Volver a canales", vm.ImmersiveReturnLabel);
        Assert.Equal(1, backend.Active);

        vm.ReturnFromImmersive();
        Assert.False(vm.IsImmersiveMode);
        Assert.Equal(1, backend.Active);
        Assert.Same(liveChannel, vm.SelectedChannel);
        Assert.Equal("Canal", vm.SearchText);

        // 2. Movie Immersive Mode
        vm.CurrentTabIndex = 5;
        await PlaybackTests.UntilAsync(() => vm.MediaItems.Count > 0);
        vm.MediaCategorySearch = "Acción";
        var movie = vm.MediaItems[0];
        vm.SelectedMedia = movie;
        await vm.PlayMediaAsync();
        Assert.True(vm.IsImmersiveMode);
        Assert.Equal("Volver a películas", vm.ImmersiveReturnLabel);
        Assert.Equal(1, backend.Active);

        vm.ReturnFromImmersive();
        Assert.False(vm.IsImmersiveMode);
        Assert.Equal(1, backend.Active);
        Assert.Same(movie, vm.SelectedMedia);
        Assert.Equal("Acción", vm.MediaCategorySearch);

        // 3. Series Immersive Mode
        vm.CurrentTabIndex = 6;
        await PlaybackTests.UntilAsync(() => vm.MediaItems.Count > 0);
        var series = vm.MediaItems[0];
        vm.SelectedMedia = series;
        await PlaybackTests.UntilAsync(() => vm.Episodes.Count > 0);
        var episode = vm.Episodes[0];
        vm.SelectedEpisode = episode;
        await vm.PlayMediaAsync();
        Assert.True(vm.IsImmersiveMode);
        Assert.True(vm.IsEpisodePlayerFocus);
        Assert.Equal("Volver a episodios", vm.ImmersiveReturnLabel);
        Assert.Equal(1, backend.Active);

        vm.ReturnFromImmersive();
        Assert.False(vm.IsImmersiveMode);
        Assert.False(vm.IsEpisodePlayerFocus);
        Assert.Equal(1, backend.Active);
        Assert.Same(series, vm.SelectedMedia);
        Assert.Same(episode, vm.SelectedEpisode);
    }

    [Fact]
    public async Task RandomEpisodePlayback_ManualAndAutoAdvance_AvoidsRepeatsFiltersInvalidAndRespectsEndOfMedia()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        var provider = new MultiSeasonSeriesProvider();
        await using var vm = new MainViewModel(
            (_, _, _) => provider,
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.SqliteContentStore(db.PathName, false),
            new StreamDesk.Probe.Infrastructure.XmlTvReader())
        {
            RandomIndex = _ => 0
        };

        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();
        await vm.ProbeContentAsync();

        vm.CurrentTabIndex = 6;
        await PlaybackTests.UntilAsync(() => vm.MediaItems.Count == 3);
        Assert.False(vm.AutoRandomNextEpisode);

        // Series 1 has 3 playable episodes across 2 seasons + 2 invalid episodes
        var mainSeries = vm.MediaItems.Single(item => item.Id == "series-multi");
        vm.SelectedMedia = mainSeries;
        await PlaybackTests.UntilAsync(() => vm.Seasons.Count == 2 && vm.CanPlayRandomEpisode);
        Assert.Equal(1, provider.GetEpisodesCalls);
        Assert.Equal("ep-1", vm.SelectedEpisode?.Id);

        // Pick 1: current is ep-1, remaining playable in cycle are [ep-2, ep-3] -> picks ep-2
        await vm.PlayRandomEpisodeAsync();
        Assert.Equal("ep-2", vm.SelectedEpisode?.Id);
        Assert.Equal("season-1", vm.Season?.Id);
        Assert.Equal(1, provider.GetEpisodesCalls);

        // Pick 2: current is ep-2, remaining unplayed in cycle is [ep-3] (in season-2) -> picks ep-3 and switches Season
        await vm.PlayRandomEpisodeAsync();
        Assert.Equal("ep-3", vm.SelectedEpisode?.Id);
        Assert.Equal("season-2", vm.Season?.Id);
        Assert.Equal(1, provider.GetEpisodesCalls);

        // Pick 3: cycle exhausted, resets cycle excluding just-played ep-3 -> candidates [ep-1, ep-2] -> picks ep-1
        await vm.PlayRandomEpisodeAsync();
        Assert.Equal("ep-1", vm.SelectedEpisode?.Id);
        Assert.Equal("season-1", vm.Season?.Id);

        // AutoRandomNextEpisode is false by default -> natural Ended does NOT open a new stream
        var opensBeforeEnded = backend.Opens.Count;
        var attempt1 = backend.Opens.Last().Id;
        backend.Emit(new(attempt1, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => vm.IsPlaying);
        backend.Emit(new(attempt1, PlaybackSignalKind.Ended));
        await PlaybackTests.UntilAsync(() => !vm.CanStopPlayback);
        await Task.Delay(30);
        Assert.Equal(opensBeforeEnded, backend.Opens.Count);

        // Enable AutoRandomNextEpisode: natural Ended advances once to random episode
        vm.AutoRandomNextEpisode = true;
        await vm.PlayMediaAsync();
        var attempt2 = backend.Opens.Last().Id;
        var opensOnStart = backend.Opens.Count;
        backend.Emit(new(attempt2, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => vm.IsPlaying);

        // Paused or Failed must NEVER trigger random advance
        backend.Emit(new(attempt2, PlaybackSignalKind.Paused));
        await PlaybackTests.UntilAsync(() => vm.IsPaused);
        Assert.Equal(opensOnStart, backend.Opens.Count);
        backend.Emit(new(attempt2, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => vm.IsPlaying);

        // Natural Ended triggers exactly 1 random episode advance
        backend.Emit(new(attempt2, PlaybackSignalKind.Ended));
        await PlaybackTests.UntilAsync(() => backend.Opens.Count == opensOnStart + 1);
        Assert.NotEqual("ep-1", vm.SelectedEpisode?.Id);
        Assert.Equal(1, backend.Peak);

        // Failure on the new attempt must NOT trigger random advance
        var attempt3 = backend.Opens.Last().Id;
        var opensAfterAuto = backend.Opens.Count;
        backend.Emit(new(attempt3, PlaybackSignalKind.Failed, PlaybackFailure.Forbidden));
        await PlaybackTests.UntilAsync(() => vm.CanRetryPlayback);
        await Task.Delay(30);
        Assert.Equal(opensAfterAuto, backend.Opens.Count);

        // Edge case: Series with 1 playable episode
        var singleSeries = vm.MediaItems.Single(item => item.Id == "series-single");
        vm.SelectedMedia = singleSeries;
        await PlaybackTests.UntilAsync(() => vm.CanPlayRandomEpisode && vm.SelectedEpisode?.Id == "single-1");
        await vm.PlayRandomEpisodeAsync();
        Assert.Equal("single-1", vm.SelectedEpisode?.Id);

        // Edge case: Series with 0 playable episodes
        await vm.StopPlaybackAsync();
        var opensBeforeEmpty = backend.Opens.Count;
        var emptySeries = vm.MediaItems.Single(item => item.Id == "series-empty");
        vm.SelectedMedia = emptySeries;
        await PlaybackTests.UntilAsync(() => !vm.ContentBusy);
        Assert.False(vm.CanPlayRandomEpisode);
        await vm.PlayRandomEpisodeAsync();
        Assert.Equal(opensBeforeEmpty, backend.Opens.Count);
        Assert.Contains("No hay episodios reproducibles", vm.ContentStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AudioAmplification_ClampsRange_PersistsPerProfile_And_KeepsNominalVolumeIntact()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        await db.AddProfileAsync("second");
        var credentials = new FixtureCredentials();
        var store = new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false);
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);

        await using (var vm = new MainViewModel(
            (_, _, _) => new VodFixtureProvider(),
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            store))
        {
            await vm.LoadProfilesAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsWorking && vm.ActiveProfile?.Id == "first");
            await vm.InitializePlaybackAsync();

            Assert.Equal(0, vm.AmplificationDb);
            Assert.Equal("0 dB (Normal)", vm.AmplificationLabel);
            Assert.False(vm.IsAmplified);

            // Unified volume supports 0..400%
            vm.Volume = 150;
            Assert.Equal(150, vm.Volume);
            Assert.Equal("150%", vm.VolumeLabel);
            Assert.Equal(4, vm.AmplificationDb);
            vm.Volume = 500;
            Assert.Equal(400, vm.Volume);
            vm.Volume = -20;
            Assert.Equal(0, vm.Volume);

            // Amplification clamps to 0..12 dB
            vm.AmplificationDb = 4;
            Assert.Equal(4, vm.AmplificationDb);
            Assert.True(vm.IsAmplified);
            Assert.Equal("+4 dB", vm.AmplificationLabel);
            await PlaybackTests.UntilAsync(() => backend.AmplificationDb == 4);

            vm.AmplificationDb = 99;
            Assert.Equal(12, vm.AmplificationDb);
            await PlaybackTests.UntilAsync(() => backend.AmplificationDb == 12);

            vm.AmplificationDb = -5;
            Assert.Equal(0, vm.AmplificationDb);
            await PlaybackTests.UntilAsync(() => backend.AmplificationDb == 0);

            // CycleAmplification steps 0 -> 2 -> 4 -> 6 -> 0
            vm.CycleAmplification();
            Assert.Equal(2, vm.AmplificationDb);
            vm.CycleAmplification();
            Assert.Equal(4, vm.AmplificationDb);

            // Switch to second profile -> resets to 0 dB
            vm.ActiveProfile = vm.Profiles.Single(p => p.Id == "second");
            await PlaybackTests.UntilAsync(() => !vm.IsWorking && vm.AmplificationDb == 0);
            vm.AmplificationDb = 6;

            // Switch back to first profile -> restores +4 dB
            vm.ActiveProfile = vm.Profiles.Single(p => p.Id == "first");
            await PlaybackTests.UntilAsync(() => !vm.IsWorking && vm.AmplificationDb == 4);
        }

        var firstLib = await store.LoadAsync("first");
        var secondLib = await store.LoadAsync("second");
        Assert.Equal(4, firstLib.AmplificationDb);
        Assert.Equal(6, secondLib.AmplificationDb);
    }

    [Fact]
    public async Task VideoLoadingIndicator_DistinguishesLoadingAndBuffering_AndHidesOnPlayPauseFailStopOrCancel()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        await using var vm = new MainViewModel(
            (_, _, _) => new VodFixtureProvider(),
            playback,
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials);

        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();
        await vm.LoadCatalogAsync();

        Assert.False(vm.IsVideoLoading);
        Assert.Equal(string.Empty, vm.VideoLoadingMessage);

        vm.SelectedChannel = vm.Channels[0];
        await vm.PlaySelectedAsync();
        var attempt = backend.Opens.Last().Id;

        // Initial Loading state
        Assert.True(vm.IsVideoLoading);
        Assert.True(vm.IsVideoLoadingIndeterminate);
        Assert.False(vm.HasVideoBufferPercent);
        Assert.Equal("Abriendo transmisión…", vm.VideoLoadingMessage);

        // Buffering without percentage (0%)
        backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 0));
        await PlaybackTests.UntilAsync(() => vm.VideoLoadingMessage == "Cargando búfer…");
        Assert.True(vm.IsVideoLoading);
        Assert.True(vm.IsVideoLoadingIndeterminate);
        Assert.False(vm.HasVideoBufferPercent);

        // Buffering with percentage (45%)
        backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 45));
        await PlaybackTests.UntilAsync(() => vm.HasVideoBufferPercent && vm.VideoBufferPercent == 45);
        Assert.True(vm.IsVideoLoading);
        Assert.False(vm.IsVideoLoadingIndeterminate);
        Assert.Equal("Cargando búfer (45%)…", vm.VideoLoadingMessage);

        // Playing hides loading immediately
        backend.Emit(new(attempt, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => vm.IsPlaying);
        Assert.False(vm.IsVideoLoading);
        Assert.Equal(string.Empty, vm.VideoLoadingMessage);

        // Rebuffering shows indicator again
        backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 60));
        await PlaybackTests.UntilAsync(() => vm.IsVideoLoading && vm.VideoBufferPercent == 60);

        // Manual Stop hides indicator immediately and ignores late signals from old attempt
        await vm.StopPlaybackAsync();
        Assert.False(vm.IsVideoLoading);
        backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 80));
        await Task.Delay(30);
        Assert.False(vm.IsVideoLoading);
    }

    private sealed class MultiSeasonSeriesProvider : IIptvProvider, IContentProvider
    {
        public int GetEpisodesCalls { get; private set; }
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamAccount("Active", 0, 1, false, DateTimeOffset.UtcNow.AddDays(30), ["ts", "m3u8"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamCategory>>([]);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<XtreamChannel>>([]);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new("https://provider.invalid/live.ts");
        public Uri CreateMediaUri(MediaItem item) => new($"https://provider.invalid/series/{item.Id}.{item.Extension}");
        public Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaCatalog([new("drama", "Drama")],
                kind == ContentKind.Series
                    ? [new("series-multi", "Serie Multi", ContentKind.Series, "drama"),
                       new("series-single", "Serie Única", ContentKind.Series, "drama"),
                       new("series-empty", "Serie Vacía", ContentKind.Series, "drama")]
                    : [],
                DateTimeOffset.UtcNow));
        public Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default) => Task.FromResult(movie);
        public Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default)
        {
            GetEpisodesCalls++;
            if (seriesId == "series-single")
            {
                return Task.FromResult(new MediaCatalog([new("s1", "Temporada 1")],
                    [new("single-1", "Episodio Único", ContentKind.Episode, "s1", "mp4", SeriesId: seriesId, Season: 1, Episode: 1)],
                    DateTimeOffset.UtcNow));
            }
            if (seriesId == "series-empty")
            {
                return Task.FromResult(new MediaCatalog([new("s1", "Temporada 1")],
                    [new("bad-ext", "Episodio Inválido", ContentKind.Episode, "s1", "exe", SeriesId: seriesId, Season: 1, Episode: 1)],
                    DateTimeOffset.UtcNow));
            }
            return Task.FromResult(new MediaCatalog(
                [new("season-1", "Temporada 1"), new("season-2", "Temporada 2")],
                [
                    new("ep-1", "Episodio 1", ContentKind.Episode, "season-1", "mp4", SeriesId: seriesId, Season: 1, Episode: 1),
                    new("ep-2", "Episodio 2", ContentKind.Episode, "season-1", "mkv", SeriesId: seriesId, Season: 1, Episode: 2),
                    new("", "Sin ID", ContentKind.Episode, "season-1", "mp4", SeriesId: seriesId, Season: 1, Episode: 3),
                    new("ep-bad", "Extensión Inválida", ContentKind.Episode, "season-2", "bat", SeriesId: seriesId, Season: 2, Episode: 1),
                    new("ep-3", "Episodio 3", ContentKind.Episode, "season-2", "mp4", SeriesId: seriesId, Season: 2, Episode: 2)
                ],
                DateTimeOffset.UtcNow));
        }
        public Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpgProgram>>([]);
        public void Dispose() { }
    }

    private sealed class VodFixtureProvider : IIptvProvider, IContentProvider
    {
        private readonly CatalogSnapshot _catalog = CatalogFixtures.Create(5);
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamAccount("Active", 0, 1, false, DateTimeOffset.UtcNow.AddDays(30), ["ts", "m3u8"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_catalog.Categories);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_catalog.Channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) =>
            new($"https://provider.invalid/live/{channel.Id}.{format}");
        public Uri CreateMediaUri(MediaItem item) =>
            new($"https://provider.invalid/movie/{item.Id}.mp4");
        public Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaCatalog([new("action", "Acción")],
                kind == ContentKind.Movie
                    ? [new("m1", "Película Demo", ContentKind.Movie, "action", "mp4")]
                    : [new("s1", "Serie Demo", ContentKind.Series, "action")],
                DateTimeOffset.UtcNow));
        public Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default) =>
            Task.FromResult(movie with { Plot = "Sinopsis demo" });
        public Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaCatalog([new("season-1", "Temporada 1")],
                [new("ep-1", "Episodio 1", ContentKind.Episode, "season-1", "mp4", SeriesId: seriesId, Season: 1, Episode: 1),
                 new("ep-2", "Episodio 2", ContentKind.Episode, "season-1", "mp4", SeriesId: seriesId, Season: 1, Episode: 2)],
                DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EpgProgram>>([]);
        public void Dispose() { }
    }
}
