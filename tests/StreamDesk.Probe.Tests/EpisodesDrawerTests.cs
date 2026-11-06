using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;
using Xunit;

namespace StreamDesk.Probe.Tests;

public sealed class EpisodesDrawerTests
{
    private static async Task<MainViewModel> CreateConnectedVmAsync(TestDatabase db, FixturePlayback? playback = null, IIptvProvider? provider = null)
    {
        var credentials = new FixtureCredentials();
        var vm = new MainViewModel(
            (_, _, _) => provider ?? new FixtureProvider(CatalogFixtures.Create(10)),
            playback ?? new FixturePlayback(),
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials);
        await vm.ConnectAsync("https://provider.invalid", "fixture-user", "fixture-password");
        await vm.LoadCatalogAsync();
        return vm;
    }

    [Fact]
    public async Task CanBrowseEpisodes_IsTrueForSeriesEpisode_AndFalseForMoviesAndLiveChannels()
    {
        using var db = new TestDatabase();
        var playback = new FixturePlayback();
        var vm = await CreateConnectedVmAsync(db, playback);

        // 1. Live TV Channel
        Assert.NotEmpty(vm.Channels);
        vm.SelectedChannel = vm.Channels[0];
        await vm.PlaySelectedAsync();
        Assert.False(vm.CanBrowseEpisodes, "Live TV should not allow episode browsing");

        // 2. Movie
        var movie = new MediaItem("movie-1", "Test Movie", ContentKind.Movie);
        vm.SelectedMedia = movie;
        await vm.PlayMediaAsync();
        Assert.False(vm.CanBrowseEpisodes, "Movies should not allow episode browsing");

        // 3. Series Episode
        var series = new MediaItem("series-1", "Test Series", ContentKind.Series, CategoryId: "1");
        var episode = new MediaItem("ep-1", "Episode 1", ContentKind.Episode, CategoryId: "1", SeriesId: "series-1", Season: 1, Episode: 1);
        vm.SelectedMedia = series;
        vm.SelectedEpisode = episode;
        await vm.PlayMediaAsync();
        Assert.True(vm.CanBrowseEpisodes, "Series episodes must allow episode browsing");

        // 4. Stopping playback
        await vm.StopPlaybackAsync();
        Assert.False(vm.CanBrowseEpisodes, "Stopped playback must not allow episode browsing");
    }

    [Fact]
    public async Task EpisodesDrawer_OpenAndClose_MutuallyClosesOtherPanels()
    {
        using var db = new TestDatabase();
        var playback = new FixturePlayback();
        var vm = await CreateConnectedVmAsync(db, playback);

        var episode = new MediaItem("ep-1", "Episode 1", ContentKind.Episode, CategoryId: "1", SeriesId: "series-1", Season: 1, Episode: 1);
        vm.SelectedEpisode = episode;
        await vm.PlayMediaAsync();
        Assert.True(vm.CanBrowseEpisodes);

        // 1. Open drawer
        await vm.OpenEpisodesDrawerAsync();
        Assert.True(vm.IsEpisodesDrawerOpen);
        Assert.False(vm.IsAudioSubtitlesOpen);
        Assert.False(vm.IsPlaybackStatsOpen);

        // 2. Opening AudioSubtitles closes drawer
        vm.ToggleAudioSubtitles();
        Assert.True(vm.IsAudioSubtitlesOpen);
        Assert.False(vm.IsEpisodesDrawerOpen);

        // 3. Reopening drawer closes AudioSubtitles
        await vm.OpenEpisodesDrawerAsync();
        Assert.True(vm.IsEpisodesDrawerOpen);
        Assert.False(vm.IsAudioSubtitlesOpen);

        // 4. Opening Stats closes drawer
        vm.TogglePlaybackStats();
        Assert.True(vm.IsPlaybackStatsOpen);
        Assert.False(vm.IsEpisodesDrawerOpen);

        // 5. Reopening drawer closes Stats
        await vm.OpenEpisodesDrawerAsync();
        Assert.True(vm.IsEpisodesDrawerOpen);
        Assert.False(vm.IsPlaybackStatsOpen);

        // 6. Manual close
        vm.CloseEpisodesDrawer();
        Assert.False(vm.IsEpisodesDrawerOpen);
    }

    [Fact]
    public void EpisodesDrawer_ExtractSeasonNumber_SortsRealNumericSeasons()
    {
        Assert.Equal(1, MainViewModel.ExtractSeasonNumber("Temporada 1"));
        Assert.Equal(2, MainViewModel.ExtractSeasonNumber("Temporada 2"));
        Assert.Equal(10, MainViewModel.ExtractSeasonNumber("Temporada 10"));
        Assert.Equal(5, MainViewModel.ExtractSeasonNumber("Season 5"));
        Assert.Equal(3, MainViewModel.ExtractSeasonNumber("T3"));
        Assert.Equal(4, MainViewModel.ExtractSeasonNumber(null, "4"));
        Assert.Equal(1, MainViewModel.ExtractSeasonNumber(null, null));
    }

    [Fact]
    public void EpisodesDrawerWidth_AdaptsToAvailablePlayerWidth()
    {
        var converter = new EpisodesDrawerWidthConverter();

        Assert.Equal(380d, converter.Convert(900d, typeof(double), string.Empty, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(520d, converter.Convert(1600d, typeof(double), string.Empty, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(200d, converter.Convert(200d, typeof(double), string.Empty, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task EpisodesDrawer_Navigation_And_CurrentEpisodeIdentification()
    {
        using var db = new TestDatabase();
        var playback = new FixturePlayback();
        var vm = await CreateConnectedVmAsync(db, playback);

        var episodesList = new List<MediaItem>
        {
            new("ep-101", "Episodio 1", ContentKind.Episode, CategoryId: "1", Extension: "mp4", SeriesId: "series-1", Season: 1, Episode: 1, Plot: "Inicio de la aventura"),
            new("ep-102", "Episodio 2", ContentKind.Episode, CategoryId: "1", Extension: "mp4", SeriesId: "series-1", Season: 1, Episode: 2, Plot: "El misterio crece"),
            new("ep-201", "Episodio 1 S2", ContentKind.Episode, CategoryId: "2", Extension: "mp4", SeriesId: "series-1", Season: 2, Episode: 1, Plot: "Nueva temporada"),
            new("ep-202", "Episodio 2 S2", ContentKind.Episode, CategoryId: "2", Extension: "mp4", SeriesId: "series-1", Season: 2, Episode: 2, Plot: "Desenlace")
        };

        var catalog = new MediaCatalog(
            [new XtreamCategory("1", "Temporada 1"), new XtreamCategory("2", "Temporada 2")],
            episodesList,
            DateTimeOffset.UtcNow);

        typeof(MainViewModel).GetField("_episodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, catalog);

        // Start playing Season 2, Episode 1
        vm.SelectedEpisode = episodesList[2];
        await vm.PlayMediaAsync();

        // 1. Open drawer: should automatically select Season 2 because currently playing episode is Season 2
        await vm.OpenEpisodesDrawerAsync();
        Assert.True(vm.IsEpisodesDrawerOpen);
        Assert.Equal("2", vm.DrawerSelectedSeason?.Id);

        // 2. Check episodes in Season 2: Episode 1 is playing, Episode 2 is not
        Assert.Equal(2, vm.DrawerEpisodes.Count);
        var playingEp = vm.DrawerEpisodes.First(e => e.Episode.Id == "ep-201");
        var otherEp = vm.DrawerEpisodes.First(e => e.Episode.Id == "ep-202");

        Assert.True(playingEp.IsPlaying);
        Assert.Equal("Reproduciendo", playingEp.PlayStatusText);
        Assert.False(otherEp.IsPlaying);
        Assert.Equal("", otherEp.PlayStatusText);

        // 3. Switch to Seasons view
        vm.SwitchToSeasonsView();
        Assert.True(vm.IsDrawerSeasonsViewOpen);
        Assert.False(vm.IsDrawerEpisodesViewOpen);
        Assert.Equal(2, vm.DrawerSeasons.Count);
        Assert.Equal("Temporada 1", vm.DrawerSeasons[0].Name);
        Assert.Equal("Temporada 2", vm.DrawerSeasons[1].Name);
        Assert.True(vm.DrawerSeasons[1].IsSelected); // Season 2 is selected

        // 4. Select Season 1: should switch back to episodes view for Season 1 WITHOUT changing current video
        vm.SelectDrawerSeason(catalog.Categories[0]);
        Assert.False(vm.IsDrawerSeasonsViewOpen);
        Assert.True(vm.IsDrawerEpisodesViewOpen);
        Assert.Equal("1", vm.DrawerSelectedSeason?.Id);
        Assert.Equal(2, vm.DrawerEpisodes.Count);
        Assert.Equal("ep-101", vm.DrawerEpisodes[0].Episode.Id);
        Assert.Equal("ep-102", vm.DrawerEpisodes[1].Episode.Id);

        // Current playing video is still ep-201
        Assert.False(vm.DrawerEpisodes[0].IsPlaying);
        Assert.False(vm.DrawerEpisodes[1].IsPlaying);

        // 5. Click an episode already playing: should NOT reload/restart
        var startSequence = playback.Snapshot.Sequence;
        await vm.PlayEpisodeFromDrawerAsync(episodesList[2]); // Ep 201
        Assert.Equal(startSequence, playback.Snapshot.Sequence); // No new playback request

        playback.FailNextPlay = true;
        await vm.PlayEpisodeFromDrawerAsync(episodesList[0]); // Simulate an unavailable episode
        Assert.True(vm.IsEpisodesDrawerOpen); // Keep the drawer available for retry/navigation
        Assert.Equal("No se pudo reproducir el episodio seleccionado.", vm.DrawerStatusMessage);
        Assert.DoesNotContain(vm.DrawerEpisodes, item => item.IsPlaying);

        // 6. Click a different episode: should play it, cancel auto-play countdown, and close drawer
        vm.IsNextEpisodeOverlayOpen = true; // Simulating auto-play open
        await vm.PlayEpisodeFromDrawerAsync(episodesList[0]); // Ep 101
        Assert.False(vm.IsNextEpisodeOverlayOpen); // Auto-play was cancelled
        Assert.False(vm.IsEpisodesDrawerOpen); // Drawer closed
        Assert.True(playback.Snapshot.Sequence > startSequence); // New playback was triggered
    }

    [Fact]
    public async Task EpisodesDrawer_PreservesWatchProgressFromStore()
    {
        using var db = new TestDatabase();
        var playback = new FixturePlayback();
        var vm = await CreateConnectedVmAsync(db, playback);

        var episode1 = new MediaItem("ep-1", "Episodio 1", ContentKind.Episode, CategoryId: "1", SeriesId: "series-1", Season: 1, Episode: 1);
        var catalog = new MediaCatalog([new XtreamCategory("1", "Temporada 1")], [episode1], DateTimeOffset.UtcNow);

        typeof(MainViewModel).GetField("_episodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, catalog);

        // Inject watch progress: 50% watched (30 min of 60 min)
        var progressList = new List<MediaProgress>
        {
            new(episode1, PositionMilliseconds: 1800000, DurationMilliseconds: 3600000, UpdatedAt: DateTimeOffset.UtcNow, Completed: false)
        };
        typeof(MainViewModel).GetField("_progress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, progressList);

        vm.SelectedEpisode = episode1;
        await vm.PlayMediaAsync();

        await vm.OpenEpisodesDrawerAsync();
        var epItem = Assert.Single(vm.DrawerEpisodes);

        Assert.True(epItem.HasProgress);
        Assert.InRange(epItem.ProgressFraction, 0.49, 0.51);
        Assert.Equal("60 min", epItem.DurationText);
    }

    [Fact]
    public async Task EpisodesDrawer_HandlesMissingMetadataAndSingleSeasonGracefully()
    {
        using var db = new TestDatabase();
        var playback = new FixturePlayback();
        var vm = await CreateConnectedVmAsync(db, playback);

        // Episode without name, without plot, without poster
        var bareEpisode = new MediaItem("ep-bare", "", ContentKind.Episode, CategoryId: "1", SeriesId: "series-min", Season: 1, Episode: 1, PosterUrl: null, Plot: null);
        var catalog = new MediaCatalog([new XtreamCategory("1", "Temporada 1")], [bareEpisode], DateTimeOffset.UtcNow);

        typeof(MainViewModel).GetField("_episodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, catalog);

        vm.SelectedEpisode = bareEpisode;
        await vm.PlayMediaAsync();

        await vm.OpenEpisodesDrawerAsync();
        var item = Assert.Single(vm.DrawerEpisodes);

        Assert.Equal(1, item.Number);
        Assert.Equal("1. Episodio 1", item.DisplayTitle);
        Assert.Null(item.PosterUrl);
        Assert.Equal("Sin sinopsis disponible.", item.Description);
        Assert.False(item.HasProgress);
        Assert.Equal("", item.DurationText);
    }
}
