using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class ViewerStoreTests
{
    [Fact]
    public async Task FavoritesPreferencesAndLastProfileSurviveRestartAndStayIsolated()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10)); await db.Store.ReplaceAsync("second", CatalogFixtures.Create(10));
        db.Execute("INSERT INTO Channels(ProfileId,ContentType,ProviderId,Name,NormalizedName) VALUES('first','vod','000003','Fixture','FIXTURE');");
        var store = new SqliteViewerStore(db.PathName, false);
        Assert.False((await store.LoadAsync("first")).AutoPlayLast);
        await store.SetFavoriteAsync("first", "live", "000003", true);
        await store.SetFavoriteAsync("first", "live", "000003", true);
        await store.SetFavoriteAsync("first", "vod", "000003", true);
        await store.SetAutoPlayAsync("first", true); await store.SetLastProfileAsync("first");
        var restarted = new SqliteViewerStore(db.PathName, false);
        var library = await restarted.LoadAsync("first"); Assert.Equal("000003", Assert.Single(library.Favorites));
        Assert.True(library.AutoPlayLast); Assert.Equal("first", await restarted.GetLastProfileAsync());
        Assert.Empty((await restarted.LoadAsync("second")).Favorites); Assert.False((await restarted.LoadAsync("second")).AutoPlayLast);
        await restarted.SetFavoriteAsync("first", "live", "000003", false);
        using var connection = db.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Favorites WHERE ContentType='vod';"; Assert.Equal(1L, command.ExecuteScalar());
        Assert.Empty((await restarted.LoadAsync("first")).Favorites);
        await db.Settings.DeleteProfileAsync("first"); Assert.Null(await restarted.GetLastProfileAsync());
        Assert.Empty((await restarted.LoadAsync("first")).Favorites); Assert.False((await restarted.LoadAsync("first")).AutoPlayLast);
    }

    [Fact]
    public async Task HistoryIsUniqueBoundedOrderedAndRemovedWithStaleChannels()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.Store.ReplaceAsync("first", CatalogFixtures.Create(70));
        var store = new SqliteViewerStore(db.PathName, false); var date = DateTimeOffset.UtcNow;
        for (var i = 0; i < 70; i++) await store.RecordPlayedAsync("first", "live", i.ToString("D6"), date.AddSeconds(i));
        var library = await store.LoadAsync("first"); Assert.Equal(50, library.Recents.Count); Assert.Equal("000069", library.Recents[0].ChannelId);
        Assert.DoesNotContain(library.Recents, item => item.ChannelId == "000019");
        await store.RecordPlayedAsync("first", "live", "000030", date.AddSeconds(100));
        await store.RecordPlayedAsync("first", "live", "000030", date.AddSeconds(1));
        var restarted = await new SqliteViewerStore(db.PathName, false).LoadAsync("first");
        Assert.Equal("000030", restarted.Recents[0].ChannelId); Assert.Equal(date.AddSeconds(100), restarted.Recents[0].PlayedAt);
        Assert.Equal(50, restarted.Recents.Count);
        await db.Store.ReplaceAsync("first", CatalogFixtures.Create(25));
        Assert.All((await store.LoadAsync("first")).Recents, item => Assert.InRange(int.Parse(item.ChannelId), 20, 24));
    }

    [Fact]
    public async Task FailedAndCancelledWritesLeaveLibraryIntact()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        var store = new SqliteViewerStore(db.PathName, false);
        db.Execute("CREATE TRIGGER FailFavorite BEFORE INSERT ON Favorites BEGIN SELECT RAISE(ABORT,'fixture'); END;");
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => store.SetFavoriteAsync("first", "live", "000003", true));
        Assert.Empty((await store.LoadAsync("first")).Favorites);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RecordPlayedAsync("first", "live", "000003", DateTimeOffset.UtcNow, cancellation.Token));
        await store.RecordPlayedAsync("first", "live", "missing", DateTimeOffset.UtcNow);
        Assert.Empty((await store.LoadAsync("first")).Recents);
    }

    [Fact]
    public async Task VersionTwoMigrationConsolidatesHistoryAndDefaultsAutoplayOff()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        db.Execute("""
            DROP TABLE PlaybackHistory; DROP TABLE ProfilePreferences; DROP TABLE AppPreferences;
            DROP TABLE MediaCache; DROP TABLE MediaProgress; DROP TABLE EpgCache;
            CREATE TABLE PlaybackHistory(Id INTEGER PRIMARY KEY,ProfileId TEXT,ContentType TEXT,ChannelId TEXT,PlayedAt TEXT);
            INSERT INTO PlaybackHistory VALUES(1,'first','live','000003','2026-10-17T12:00:00Z'),(2,'first','live','000003','2026-10-18T12:00:00Z'),(3,'first','live','missing','2026-10-18T12:00:00Z');
            UPDATE SchemaVersion SET Version=2;
            """);
        await db.Settings.InitializeAsync(); await db.Settings.InitializeAsync();
        var library = await new SqliteViewerStore(db.PathName, false).LoadAsync("first");
        var recent = Assert.Single(library.Recents); Assert.Equal("000003", recent.ChannelId);
        Assert.Equal(DateTimeOffset.Parse("2026-10-18T12:00:00Z"), recent.PlayedAt); Assert.False(library.AutoPlayLast);
    }
}
