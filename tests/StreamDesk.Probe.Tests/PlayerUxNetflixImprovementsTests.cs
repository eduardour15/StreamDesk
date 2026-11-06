using System.Globalization;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;
using Xunit;

namespace StreamDesk.Probe.Tests;

public sealed class PlayerUxNetflixImprovementsTests
{
    [Fact]
    public void MediaTrackParser_ParsesAudioTracks_WithLanguageRegionAndMetadata()
    {
        var rawSpanishLatam = new MediaTrack(1, "Español (Latinoamérica) [spa] 5.1");
        var parsed1 = MediaTrackParser.ParseAudioTrack(rawSpanishLatam);
        Assert.Equal("es", parsed1.LanguageCode);
        Assert.Equal("Latinoamérica", parsed1.Region);
        Assert.Equal("5.1", parsed1.Channels);
        Assert.False(parsed1.IsOriginal);
        Assert.False(parsed1.IsDisabled);
        Assert.Contains("Español", parsed1.DisplayName);
        Assert.Contains("Latinoamérica", parsed1.DisplayName);

        var rawEnglishOriginal = new MediaTrack(2, "English [eng] (Original)");
        var parsed2 = MediaTrackParser.ParseAudioTrack(rawEnglishOriginal);
        Assert.Equal("en", parsed2.LanguageCode);
        Assert.True(parsed2.IsOriginal);
        Assert.Contains("Inglés", parsed2.DisplayName);
        Assert.Contains("Original", parsed2.DisplayName);

        var rawJapanese = new MediaTrack(3, "Japanese [jpn]");
        var parsed3 = MediaTrackParser.ParseAudioTrack(rawJapanese);
        Assert.Equal("ja", parsed3.LanguageCode);
        Assert.Equal("Japonés", parsed3.DisplayName);

        var rawGeneric = new MediaTrack(4, "Track 4");
        var parsed4 = MediaTrackParser.ParseAudioTrack(rawGeneric);
        Assert.Equal("", parsed4.LanguageCode);
        Assert.Equal("Pista 4", parsed4.DisplayName);
    }

    [Fact]
    public void MediaTrackParser_ParsesSubtitleTracks_WithSdhAndDisabledOptions()
    {
        var rawDisabled = new MediaTrack(-1, "Disable");
        var parsedDisabled = MediaTrackParser.ParseSubtitleTrack(rawDisabled);
        Assert.True(parsedDisabled.IsDisabled);
        Assert.Equal("Desactivados", parsedDisabled.DisplayName);

        var rawSpanish = new MediaTrack(1, "Spanish [spa]");
        var parsedSpanish = MediaTrackParser.ParseSubtitleTrack(rawSpanish);
        Assert.False(parsedSpanish.IsDisabled);
        Assert.Equal("es", parsedSpanish.LanguageCode);
        Assert.Equal("Español", parsedSpanish.DisplayName);

        var rawEnglishCC = new MediaTrack(2, "English [eng] (CC)");
        var parsedEnglishCC = MediaTrackParser.ParseSubtitleTrack(rawEnglishCC);
        Assert.False(parsedEnglishCC.IsDisabled);
        Assert.Equal("en", parsedEnglishCC.LanguageCode);
        Assert.True(parsedEnglishCC.IsSdh);
        Assert.Contains("Inglés", parsedEnglishCC.DisplayName);
        Assert.Contains("CC", parsedEnglishCC.DisplayName);
    }

    [Fact]
    public void TrackMatcher_MatchesExactRegion_ThenLanguage_ThenTitle()
    {
        var tracks = new List<MediaTrackInfo>
        {
            MediaTrackParser.ParseAudioTrack(new MediaTrack(1, "English [eng]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(2, "Español (España) [spa]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(3, "Español (Latinoamérica) [spa]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(4, "Japanese [jpn]"))
        };

        // 1. Preference with region matches exact region
        var prefLatam = new MediaTrackPreference("series-1", ContentKind.Series, "es", "Latinoamérica", "Español", null, null, null, false, DateTimeOffset.UtcNow);
        var match1 = MediaTrackParser.FindBestAudioMatch(tracks, prefLatam);
        Assert.NotNull(match1);
        Assert.Equal(3, match1.Id);

        // 2. Preference with language but no region matches first Spanish
        var prefAnySpanish = new MediaTrackPreference("series-1", ContentKind.Series, "es", null, "Español", null, null, null, false, DateTimeOffset.UtcNow);
        var match2 = MediaTrackParser.FindBestAudioMatch(tracks, prefAnySpanish);
        Assert.NotNull(match2);
        Assert.Equal("es", match2.LanguageCode);

        // 3. Fallback when preferred language is missing: returns null without crashing
        var prefFrench = new MediaTrackPreference("series-1", ContentKind.Series, "fr", null, "French", null, null, null, false, DateTimeOffset.UtcNow);
        var match3 = MediaTrackParser.FindBestAudioMatch(tracks, prefFrench, fallbackLanguage: null);
        Assert.Null(match3);

        // 4. Fallback to global user language ("en")
        var match4 = MediaTrackParser.FindBestAudioMatch(tracks, prefFrench, fallbackLanguage: "en");
        Assert.NotNull(match4);
        Assert.Equal(1, match4.Id);
    }

    [Fact]
    public void TrackMatcher_MatchesSubtitlesDisabledAndLanguages()
    {
        var subTracks = new List<MediaTrackInfo>
        {
            new(-1, "Disable", "Desactivados", "", IsDisabled: true),
            MediaTrackParser.ParseSubtitleTrack(new MediaTrack(1, "Español [spa]")),
            MediaTrackParser.ParseSubtitleTrack(new MediaTrack(2, "English [eng]"))
        };

        // 1. Subtitles disabled
        var prefDisabled = new MediaTrackPreference("s-1", ContentKind.Series, "es", null, null, "en", null, null, SubtitlesDisabled: true, DateTimeOffset.UtcNow);
        var matchDisabled = MediaTrackParser.FindBestSubtitleMatch(subTracks, prefDisabled);
        Assert.NotNull(matchDisabled);
        Assert.True(matchDisabled.IsDisabled);
        Assert.Equal(-1, matchDisabled.Id);

        // 2. Subtitles enabled in Spanish
        var prefSpanishSubs = new MediaTrackPreference("s-1", ContentKind.Series, "en", null, null, "es", null, "Español", SubtitlesDisabled: false, DateTimeOffset.UtcNow);
        var matchSpanish = MediaTrackParser.FindBestSubtitleMatch(subTracks, prefSpanishSubs);
        Assert.NotNull(matchSpanish);
        Assert.Equal(1, matchSpanish.Id);
    }

    [Fact]
    public async Task SqliteContentStore_PersistsTrackPreferences_ForSeriesAndMoviesIndependently()
    {
        using var db = new TestDatabase();
        await db.AddProfileAsync("profile-1");
        var store = new SqliteContentStore(db.PathName, false);

        // 1. Save preference for Series Breaking Bad
        var seriesPref = new MediaTrackPreference(
            ContentId: "series-breaking-bad",
            Kind: ContentKind.Series,
            AudioLanguage: "es",
            AudioRegion: "Latinoamérica",
            AudioTitle: "Español (Latinoamérica)",
            SubtitleLanguage: "",
            SubtitleRegion: null,
            SubtitleTitle: "Desactivados",
            SubtitlesDisabled: true,
            UpdatedAt: DateTimeOffset.UtcNow);

        await store.SaveTrackPreferenceAsync("profile-1", seriesPref);

        // 2. Save independent preference for Movie Inception
        var moviePref = new MediaTrackPreference(
            ContentId: "movie-inception",
            Kind: ContentKind.Movie,
            AudioLanguage: "en",
            AudioRegion: null,
            AudioTitle: "Inglés (Original)",
            SubtitleLanguage: "es",
            SubtitleRegion: null,
            SubtitleTitle: "Español",
            SubtitlesDisabled: false,
            UpdatedAt: DateTimeOffset.UtcNow);

        await store.SaveTrackPreferenceAsync("profile-1", moviePref);

        // 3. Re-read using a fresh store instance (simulating restart)
        var restartedStore = new SqliteContentStore(db.PathName, false);
        var loadedSeries = await restartedStore.LoadTrackPreferenceAsync("profile-1", "series-breaking-bad", ContentKind.Series);
        var loadedMovie = await restartedStore.LoadTrackPreferenceAsync("profile-1", "movie-inception", ContentKind.Movie);

        Assert.NotNull(loadedSeries);
        Assert.Equal("series-breaking-bad", loadedSeries.ContentId);
        Assert.Equal("es", loadedSeries.AudioLanguage);
        Assert.Equal("Latinoamérica", loadedSeries.AudioRegion);
        Assert.True(loadedSeries.SubtitlesDisabled);

        Assert.NotNull(loadedMovie);
        Assert.Equal("movie-inception", loadedMovie.ContentId);
        Assert.Equal("en", loadedMovie.AudioLanguage);
        Assert.Equal("es", loadedMovie.SubtitleLanguage);
        Assert.False(loadedMovie.SubtitlesDisabled);
    }

    private sealed class NetflixTestContentProvider : IIptvProvider, IContentProvider
    {
        private readonly CatalogSnapshot _snapshot = CatalogFixtures.Create(10);
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot.Categories);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot.Channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new("https://provider.invalid/fixture.ts");
        public Uri CreateMediaUri(MediaItem item) => new("https://provider.invalid/fixture.mp4");
        public Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaCatalog([new("1", "General")], [new(kind == ContentKind.Movie ? "m-1" : "s-1", kind == ContentKind.Movie ? "Pelicula" : "Serie", kind, "1")], DateTimeOffset.UtcNow));
        public Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default) => Task.FromResult(movie with { Plot = "Sinopsis" });
        public Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default) => Task.FromResult(new MediaCatalog([new("1", "T1")], [new("ep-1", "Episodio 1", ContentKind.Episode, "1", SeriesId: seriesId, Season: 1, Episode: 1)], DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EpgProgram>>([]);
        public void Dispose() { }
    }

    private static MainViewModel CreateVm(TestDatabase db, FixturePlayback? playback = null, IIptvProvider? provider = null)
    {
        var credentials = new FixtureCredentials();
        return new MainViewModel(
            (_, _, _) => provider ?? new NetflixTestContentProvider(),
            playback ?? new FixturePlayback(),
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            viewer: null,
            content: new SqliteContentStore(db.PathName, pooling: false));
    }

    [Fact]
    public void AutoPlay_FindNextEpisode_ResolvesSameSeasonAndSeasonTransition()
    {
        using var db = new TestDatabase();
        using var vm = CreateVm(db);

        var episodes = new List<MediaItem>
        {
            new("ep-101", "Ep 1", ContentKind.Episode, CategoryId: "1", SeriesId: "series-1", Season: 1, Episode: 1),
            new("ep-102", "Ep 2", ContentKind.Episode, CategoryId: "1", SeriesId: "series-1", Season: 1, Episode: 2),
            new("ep-103", "Ep 3", ContentKind.Episode, CategoryId: "1", SeriesId: "series-1", Season: 1, Episode: 3),
            new("ep-201", "Ep 1", ContentKind.Episode, CategoryId: "2", SeriesId: "series-1", Season: 2, Episode: 1),
            new("ep-202", "Ep 2", ContentKind.Episode, CategoryId: "2", SeriesId: "series-1", Season: 2, Episode: 2)
        };

        var catalog = new MediaCatalog(
            [new XtreamCategory("1", "Temporada 1"), new XtreamCategory("2", "Temporada 2")],
            episodes,
            DateTimeOffset.UtcNow);

        // inject episodes via Reflection
        typeof(MainViewModel).GetField("_episodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, catalog);

        // 1. Next episode within Season 1
        var nextFromEp1 = vm.FindNextEpisode(episodes[0]);
        Assert.NotNull(nextFromEp1);
        Assert.Equal("ep-102", nextFromEp1.Id);
        Assert.Equal(1, nextFromEp1.Season);
        Assert.Equal(2, nextFromEp1.Episode);

        // 2. Next episode from last episode of Season 1 -> Season 2 Ep 1
        var nextFromS1End = vm.FindNextEpisode(episodes[2]);
        Assert.NotNull(nextFromS1End);
        Assert.Equal("ep-201", nextFromS1End.Id);
        Assert.Equal(2, nextFromS1End.Season);
        Assert.Equal(1, nextFromS1End.Episode);

        // 3. Next episode from last episode of Series -> null (Series completed)
        var nextFromSeriesEnd = vm.FindNextEpisode(episodes[4]);
        Assert.Null(nextFromSeriesEnd);

        // 4. Movie never has next episode
        var movie = new MediaItem("movie-1", "A Movie", ContentKind.Movie);
        Assert.Null(vm.FindNextEpisode(movie));
    }

    [Fact]
    public void AutoPlay_Countdown_CanBeCancelled_AndMaintainsState()
    {
        using var db = new TestDatabase();
        using var vm = CreateVm(db);
        vm.IsNextEpisodeOverlayOpen = true;

        vm.CancelNextEpisodeAutoPlay();

        Assert.False(vm.IsNextEpisodeOverlayOpen);
        Assert.Contains("cancelada", vm.ContentStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrackSelection_RecognizesReversedTrackOrder_InNextEpisode()
    {
        // Episode 1 available tracks:
        // ID 1: Spanish (Latinoamérica)
        // ID 2: English
        var ep1Tracks = new List<MediaTrackInfo>
        {
            MediaTrackParser.ParseAudioTrack(new MediaTrack(1, "Español (Latinoamérica) [spa]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(2, "English [eng]"))
        };

        // User selected Spanish (Latinoamérica) in Episode 1
        var pref = new MediaTrackPreference("series-100", ContentKind.Series, "es", "Latinoamérica", "Español (Latinoamérica)", null, null, null, false, DateTimeOffset.UtcNow);

        // Episode 2 available tracks (REVERSED ORDER!):
        // ID 1: English
        // ID 2: Japanese
        // ID 3: Español (Latinoamérica)
        var ep2Tracks = new List<MediaTrackInfo>
        {
            MediaTrackParser.ParseAudioTrack(new MediaTrack(1, "English [eng]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(2, "Japanese [jpn]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(3, "Español (Latinoamérica) [spa]"))
        };

        // Matcher must recognize Spanish by language and region characteristics, choosing ID 3!
        var matched = MediaTrackParser.FindBestAudioMatch(ep2Tracks, pref);
        Assert.NotNull(matched);
        Assert.Equal(3, matched.Id);
        Assert.Equal("es", matched.LanguageCode);
        Assert.Equal("Latinoamérica", matched.Region);
    }

    [Fact]
    public void TrackSelection_WhenPreferredLanguageMissing_MaintainsPreferenceForLaterEpisode()
    {
        var pref = new MediaTrackPreference("series-100", ContentKind.Series, "ja", null, "Japonés", null, null, null, false, DateTimeOffset.UtcNow);

        // Episode 2: Only has English and Spanish (Japanese is missing)
        var ep2Tracks = new List<MediaTrackInfo>
        {
            MediaTrackParser.ParseAudioTrack(new MediaTrack(1, "English [eng]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(2, "Español [spa]"))
        };

        var matchEp2 = MediaTrackParser.FindBestAudioMatch(ep2Tracks, pref, fallbackLanguage: null);
        Assert.Null(matchEp2); // Does not force a wrong track, keeps player default

        // Episode 3: Japanese is available again (at ID 5)!
        var ep3Tracks = new List<MediaTrackInfo>
        {
            MediaTrackParser.ParseAudioTrack(new MediaTrack(1, "English [eng]")),
            MediaTrackParser.ParseAudioTrack(new MediaTrack(5, "Japanese [jpn]"))
        };

        var matchEp3 = MediaTrackParser.FindBestAudioMatch(ep3Tracks, pref, fallbackLanguage: null);
        Assert.NotNull(matchEp3);
        Assert.Equal(5, matchEp3.Id);
        Assert.Equal("ja", matchEp3.LanguageCode);
    }

    [Fact]
    public void PlaybackStats_ToggleAndClose_MutuallyClosesAudioSubtitles()
    {
        using var db = new TestDatabase();
        using var vm = CreateVm(db);

        Assert.False(vm.IsPlaybackStatsOpen);
        Assert.False(vm.IsAudioSubtitlesOpen);

        // Toggle stats open
        vm.TogglePlaybackStats();
        Assert.True(vm.IsPlaybackStatsOpen);
        Assert.False(vm.IsAudioSubtitlesOpen);

        // Opening AudioSubtitles closes stats
        vm.ToggleAudioSubtitles();
        Assert.True(vm.IsAudioSubtitlesOpen);
        Assert.False(vm.IsPlaybackStatsOpen);

        // Opening stats closes AudioSubtitles
        vm.TogglePlaybackStats();
        Assert.True(vm.IsPlaybackStatsOpen);
        Assert.False(vm.IsAudioSubtitlesOpen);

        // Close stats
        vm.ClosePlaybackStats();
        Assert.False(vm.IsPlaybackStatsOpen);
    }

    [Fact]
    public void PlaybackStats_FormattedProperties_ReturnReadableStrings()
    {
        using var db = new TestDatabase();
        using var vm = CreateVm(db);

        Assert.NotNull(vm.StatsBitrateText);
        Assert.NotNull(vm.StatsFpsText);
        Assert.NotNull(vm.StatsBufferText);
        Assert.NotNull(vm.StatsRebufferText);
        Assert.NotNull(vm.StatsDecoderText);
        Assert.NotNull(vm.StatsFormatText);

        Assert.Contains(vm.SoftwareDecoding ? "Software" : "Hardware", vm.StatsDecoderText);
    }

    [Fact]
    public async Task PlayerControls_IsLivePlayback_ReflectsLiveStreamOnly_NeverVodOrIdle()
    {
        using var db = new TestDatabase();
        await db.AddProfileAsync("first");
        var playback = new FixturePlayback();
        await using var vm = CreateVm(db, playback);
        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();

        // 1. Idle state: CanSeek is false, but IsLivePlayback must NOT show live badge
        Assert.False(vm.CanSeek);
        Assert.False(vm.IsLivePlayback);

        // 2. Playing a Live TV channel: IsLivePlayback is true
        await vm.LoadCatalogAsync();
        Assert.NotEmpty(vm.Channels);
        vm.SelectedChannel = vm.Channels[0];
        await vm.PlaySelectedAsync();
        Assert.True(vm.IsLivePlayback);

        // 3. Playing a VOD Movie: IsLivePlayback is strictly false
        await vm.AutoSyncCatalogAsync(true);
        Assert.True(vm.HasMovies);
        vm.CurrentTabIndex = 5;
        for (var i = 0; i < 50 && vm.MediaItems.Count == 0; i++) await Task.Delay(20);
        Assert.NotEmpty(vm.MediaItems);
        vm.SelectedMedia = vm.MediaItems[0];
        await vm.PlayMediaAsync();
        Assert.False(vm.IsLivePlayback);
    }

    [Fact]
    public async Task AudioSubtitleSelector_SelectionKeepsPanelOpen_AndEmitsFeedback()
    {
        using var db = new TestDatabase();
        var playback = new FixturePlayback();
        await using var vm = CreateVm(db, playback);

        // Start playback so _playback is active
        await playback.PlayAsync(new Uri("http://127.0.0.1/test.mp4"));

        // User opens Audio and Subtitles panel
        vm.IsAudioSubtitlesOpen = true;
        Assert.True(vm.IsAudioSubtitlesOpen);

        var audioTrack = new MediaTrackInfo(1, "Español (Latinoamérica) [spa]", "Español", "Latinoamérica", "es", false, false, false);
        var subTrack = new MediaTrackInfo(2, "English [eng]", "Inglés", "", "en", false, false, false);

        // Select audio track: must NOT close the panel!
        await vm.SelectAudioTrackInfoAsync(audioTrack);
        Assert.True(vm.IsAudioSubtitlesOpen, "The panel must remain open after selecting an audio track.");
        Assert.True(vm.HasAudioSubtitlesFeedback);
        Assert.Contains("Español", vm.AudioSubtitlesFeedback);

        // Select subtitle track: must NOT close the panel!
        await vm.SelectSubtitleTrackInfoAsync(subTrack);
        Assert.True(vm.IsAudioSubtitlesOpen, "The panel must remain open after selecting a subtitle track.");
        Assert.True(vm.HasAudioSubtitlesFeedback);
        Assert.Contains("Inglés", vm.AudioSubtitlesFeedback);

        // Panel only closes when explicitly dismissed
        vm.CloseAudioSubtitles();
        Assert.False(vm.IsAudioSubtitlesOpen);
    }

    [Fact]
    public async Task CatalogAutoSync_PopulatesMoviesAndSeries_AndAvoidsDuplicateConcurrentRuns()
    {
        using var db = new TestDatabase();
        await db.AddProfileAsync("first");
        await using var vm = CreateVm(db);
        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();

        Assert.False(vm.HasMovies);
        Assert.False(vm.HasSeries);

        // Run auto sync
        var success = await vm.AutoSyncCatalogAsync(true);
        Assert.True(success);
        Assert.True(vm.HasMovies);
        Assert.True(vm.HasSeries);
        Assert.False(vm.IsCatalogSyncing);

        // Subsequent non-forced auto sync within 24h exits early as up-to-date
        var secondRun = await vm.AutoSyncCatalogAsync(false);
        Assert.True(secondRun);
    }
}
