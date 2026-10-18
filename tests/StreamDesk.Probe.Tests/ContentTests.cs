using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class ContentTests
{
    internal static XtreamApiClient Provider(Func<string, string> response) => new("https://provider.invalid", "fixture-user", "fixture-password", maxRetries: 0,
        handler: new XtreamPhase2Tests.Handler((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(request.RequestUri!.Query.Contains("action=", StringComparison.Ordinal) ? response(request.RequestUri.Query) : """{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}""") })));

    [Fact]
    public async Task MovieCatalogPreservesIdsSanitizesOptionalFieldsAndBuildsOnlyOwnedUris()
    {
        using var provider = Provider(query => query.Contains("categories", StringComparison.Ordinal) ? """[{"category_id":"01","category_name":"Drama"}]""" :
            query.Contains("get_vod_info", StringComparison.Ordinal) ? """{"info":{"plot":""},"movie_data":{"container_extension":"mkv"}}""" : """
            [{"stream_id":"0042","name":"Película","category_id":"01","container_extension":"mp4","stream_icon":"https://images.invalid/cover.png?token=fixture-password","direct_source":"https://untrusted.invalid/ignore"},
             {"stream_id":"0042","name":"Duplicado","container_extension":"mp4"}]
            """);
        await provider.AuthenticateAsync();
        var catalog = await provider.GetMediaCatalogAsync(ContentKind.Movie); var movie = Assert.Single(catalog.Items);
        Assert.Equal("0042", movie.Id); Assert.Null(movie.PosterUrl); Assert.Equal("01", movie.CategoryId);
        var uri = provider.CreateMediaUri(movie); Assert.Equal("provider.invalid", uri.Host); Assert.EndsWith("/0042.mp4", uri.AbsolutePath, StringComparison.Ordinal);
        var info = await provider.GetMovieInfoAsync(movie); Assert.Equal("mkv", info.Extension); Assert.Equal("Sin sinopsis disponible.", info.Description);
        var error = Assert.Throws<XtreamApiException>(() => provider.CreateMediaUri(movie with { Extension = "../secret" }));
        Assert.DoesNotContain("fixture-password", error.ToString(), StringComparison.Ordinal);
    }
    [Fact]
    public async Task EmptyCatalogRemainsEmptyAndMalformedCatalogIsRejected()
    {
        using var empty = Provider(_ => "[]"); await empty.AuthenticateAsync(); Assert.Empty((await empty.GetMediaCatalogAsync(ContentKind.Series)).Items);
        using var malformed = Provider(query => query.Contains("categories", StringComparison.Ordinal) ? "[]" : """[{"stream_id":null,"name":"Missing ID"}]""");
        await malformed.AuthenticateAsync(); await Assert.ThrowsAsync<XtreamApiException>(() => malformed.GetMediaCatalogAsync(ContentKind.Movie));
    }
    [Fact]
    public async Task EpisodesDeduplicateWithinSeasonPreserveEmptySeasonsAndComposeKeys()
    {
        using var provider = Provider(_ => """
            {"seasons":[{"season_number":1},{"season_number":2},{"season_number":3}],"episodes":{
            "1":[{"id":"7","title":"Uno","episode_num":1,"container_extension":"mkv"},{"id":"7","title":"Duplicado"}],
            "2":[{"id":"7","title":"Otro","episode_num":2,"container_extension":"mp4"}],"3":[]}}
            """);
        await provider.AuthenticateAsync(); var episodes = await provider.GetEpisodesAsync("001");
        Assert.Equal(2, episodes.Items.Count); Assert.Equal(3, episodes.Categories.Count);
        Assert.NotEqual(episodes.Items[0].StorageKey, episodes.Items[1].StorageKey); Assert.All(episodes.Items, item => Assert.Equal("001", item.SeriesId));
        Assert.Contains("/series/", provider.CreateMediaUri(episodes.Items[0]).AbsolutePath, StringComparison.Ordinal);
    }
    [Fact]
    public async Task EpgUsesUnixUtcDecodesTitlesAndOmitsAmbiguousTimes()
    {
        using var provider = Provider(_ => """
            {"epg_listings":[{"title":"Tm90aWNpYXM=","description":"UmVzdW1lbg==","start_timestamp":"1792328400","stop_timestamp":"1792332000"},
            {"title":"Ambigua","start":"2026-10-18 09:00:00","end":"2026-10-18 10:00:00"},
            {"title":"Invertida","start_timestamp":"1792332000","stop_timestamp":"1792328400"}]}
            """);
        await provider.AuthenticateAsync(); var item = Assert.Single(await provider.GetEpgAsync("42"));
        Assert.Equal("Noticias", item.Title); Assert.Equal("Resumen", item.Description); Assert.Equal(TimeSpan.Zero, item.StartUtc.Offset);
        Assert.Equal(1792328400, item.StartUtc.ToUnixTimeSeconds());
    }
    [Fact]
    public async Task XmlTvConvertsOffsetsAroundDstAndRejectsEntitiesAndMalformedXml()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            <tv><programme channel="match" start="20261025023000 +0200" stop="20261025023000 +0100"><title>Antes del cambio</title></programme>
            <programme channel="other" start="20261025023000 +0200" stop="20261025023000 +0100"><title>Otro</title></programme></tv>
            """));
        var program = Assert.Single(await XmlTvReader.ReadChannelAsync(stream, "match"));
        Assert.Equal(TimeSpan.FromHours(1), program.EndUtc - program.StartUtc); Assert.Equal(0, program.StartUtc.Hour);
        foreach (var xml in new[] { "<tv><broken>", "<!DOCTYPE tv [<!ENTITY x SYSTEM 'file:///nonexistent-fixture'>]><tv>&x;</tv>" })
        {
            using var bad = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            await Assert.ThrowsAnyAsync<XmlException>(() => XmlTvReader.ReadChannelAsync(bad, "match"));
        }
    }
    [Fact]
    public async Task CatalogAndProgressRehydrateStayIsolatedAndDoNotContainSourceUrls()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        var store = new SqliteContentStore(db.PathName, false);
        var movie = new MediaItem("7", "Película", ContentKind.Movie, Extension: "mp4", Plot: "Ver https://untrusted.invalid/movie/fixture-user/fixture-password/7.mp4", PosterUrl: "https://images.invalid/a?secret=fixture-password");
        await store.SaveCatalogAsync("first", ContentKind.Movie, "", new([], [movie], DateTimeOffset.UtcNow));
        await store.SaveProgressAsync("first", new(movie, 60000, 120000, DateTimeOffset.UtcNow));
        var restarted = new SqliteContentStore(db.PathName, false);
        Assert.Single((await restarted.LoadCatalogAsync("first", ContentKind.Movie))!.Items);
        Assert.Null(await restarted.LoadCatalogAsync("second", ContentKind.Movie)); Assert.Empty(await restarted.LoadProgressAsync("second"));
        Assert.Equal(60000, Assert.Single(await restarted.LoadProgressAsync("first")).PositionMilliseconds);
        using var connection = db.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT Payload FROM MediaProgress UNION ALL SELECT Payload FROM MediaCache;";
        using (var reader = command.ExecuteReader()) while (reader.Read()) Assert.DoesNotContain("fixture-password", reader.GetString(0), StringComparison.Ordinal);
        await db.Settings.DeleteProfileAsync("first"); Assert.Empty(await restarted.LoadProgressAsync("first"));
    }
    [Fact]
    public async Task CacheExpiresBoundsRowsAndLeavesLiveFavoritesUntouched()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        db.Execute("INSERT INTO Favorites VALUES('first','live','000003','2026-10-18T00:00:00Z');");
        var clock = new TestClock(DateTimeOffset.UtcNow); var store = new SqliteContentStore(db.PathName, false, clock);
        for (var i = 0; i < 15; i++) await store.SaveCatalogAsync("first", ContentKind.Episode, i.ToString(), new([], [], clock.GetUtcNow()));
        using var connection = db.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM MediaCache;"; Assert.Equal(12L, command.ExecuteScalar());
        await store.SaveEpgAsync("first", "7", [new("Programa", clock.GetUtcNow(), clock.GetUtcNow().AddHours(1))]);
        Assert.NotNull(await store.LoadEpgAsync("first", "7")); clock.Utc = clock.Utc.AddMinutes(6); Assert.Null(await store.LoadEpgAsync("first", "7"));
        clock.Utc = clock.Utc.AddDays(8); Assert.Null(await store.LoadCatalogAsync("first", ContentKind.Episode, "14"));
        await store.SaveCatalogAsync("first", ContentKind.Movie, "", new([], [], clock.GetUtcNow()));
        command.CommandText = "SELECT COUNT(*) FROM MediaCache;"; Assert.Equal(1L, command.ExecuteScalar());
        Assert.Contains("000003", (await db.Store.LoadAsync("first"))!.FavoriteChannelIds!);
    }
    [Fact]
    public async Task ProgressIsBoundedAndEpisodeKeysCannotCollideAcrossSeriesOrSeasons()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); var store = new SqliteContentStore(db.PathName, false);
        for (var i = 0; i < 205; i++) await store.SaveProgressAsync("first", new(new(i.ToString(), "Fixture", ContentKind.Movie), i, 1000, DateTimeOffset.UtcNow.AddSeconds(i)));
        Assert.Equal(200, (await store.LoadProgressAsync("first")).Count);
        var episode = new MediaItem("7", "Episodio", ContentKind.Episode, SeriesId: "s1", Season: 1);
        Assert.NotEqual(episode.StorageKey, (episode with { SeriesId = "s2" }).StorageKey); Assert.NotEqual(episode.StorageKey, (episode with { Season = 2 }).StorageKey);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveProgressAsync("first", new(episode, 0, 0, DateTimeOffset.UtcNow), canceled.Token));
    }
    [Fact]
    public async Task OnDemandNaturalEndDoesNotReconnectAndSeekAndTracksAreValidated()
    {
        var backend = new ControlledBackend { Details = new(2000, 10000, true, [new(3, "Español")], [new(-1, "Desactivados"), new(2, "Español")]) };
        await using var playback = new PlaybackService(backend, new(TimeSpan.FromSeconds(5), TimeSpan.Zero));
        playback.Options = new(OnDemand: true, StartPositionMilliseconds: 2000);
        await playback.PlayAsync(new Uri("https://provider.invalid/fixture.mp4")); var id = backend.Opens.Last().Id;
        backend.Emit(new(id, PlaybackSignalKind.Playing)); await PlaybackTests.UntilAsync(() => playback.Snapshot.State == PlaybackState.Playing);
        Assert.True(await playback.SeekAsync(999999)); Assert.Equal(10000, backend.Sought);
        Assert.False(await playback.SelectTrackAsync(false, 99)); Assert.True(await playback.SelectTrackAsync(true, -1));
        backend.Details = backend.Details with { CanSeek = false }; Assert.False(await playback.SeekAsync(500));
        backend.Emit(new(id, PlaybackSignalKind.Ended)); await PlaybackTests.UntilAsync(() => playback.Snapshot.EndOfMedia);
        Assert.Single(backend.Opens); Assert.False(playback.HasActiveMedia);
    }
    [Fact]
    public async Task OnDemandRecoveryUsesLatestPositionAndStillAllowsOnlyOneStream()
    {
        var backend = new ControlledBackend { Details = new(45000, 120000, true, [], []) };
        await using var playback = new PlaybackService(backend, new(TimeSpan.FromSeconds(5), TimeSpan.Zero)); playback.Options = new(OnDemand: true);
        await playback.PlayAsync(new Uri("https://provider.invalid/fixture.mp4")); var id = backend.Opens.Last().Id;
        backend.Emit(new(id, PlaybackSignalKind.Playing)); await PlaybackTests.UntilAsync(() => playback.Snapshot.State == PlaybackState.Playing);
        backend.Emit(new(id, PlaybackSignalKind.Failed, PlaybackFailure.Network)); await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
        Assert.Equal(45000, backend.Opens.Last().Options.StartPositionMilliseconds); Assert.Equal(1, backend.Peak);
    }
    private sealed class TestClock(DateTimeOffset utc) : TimeProvider { public DateTimeOffset Utc { get; set; } = utc; public override DateTimeOffset GetUtcNow() => Utc; }
}
