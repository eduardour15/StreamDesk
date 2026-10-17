using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void SearchesFiveThousandChannelsWithAccentsCategoriesFavoritesAndEmptyResults()
    {
        var fixture = CatalogFixtures.Create();
        var search = new CatalogSearch(fixture.Channels);
        Assert.Equal(5000, search.Filter("", null, false, null).Count);
        Assert.Equal("004999", Assert.Single(search.Filter("MUSICA aguila", "music", false, null)).Id);
        Assert.Equal(2500, search.Filter("", "news", false, null).Count);
        Assert.Empty(search.Filter("", "empty", false, null));
        Assert.Equal(2, search.Filter("", null, true, fixture.FavoriteChannelIds).Count);
        Assert.Equal(2, search.Filter("REPETIDO", null, false, null).Count);
        Assert.Empty(search.Filter("inexistente", null, false, null));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => search.Filter("", null, false, null, cancellation.Token));
    }

    [Fact]
    public async Task CacheSurvivesRelaunchAndSeparatesProfileAndContentType()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        var fixture = CatalogFixtures.Create();
        await db.Store.ReplaceAsync("first", fixture);
        await db.Store.ReplaceAsync("second", CatalogFixtures.Create(12));
        db.Execute("INSERT INTO Channels(ProfileId,ContentType,ProviderId,Name,NormalizedName) VALUES('first','vod','000000','VOD','VOD');");
        await db.Store.ReplaceAsync("first", fixture);

        var restarted = new SqliteCatalogStore(db.PathName, pooling: false);
        var loaded = (await restarted.LoadAsync("first"))!;
        Assert.Equal(5000, loaded.Channels.Count);
        Assert.Equal(fixture.SyncedAt, loaded.SyncedAt);
        Assert.Equal(12, (await restarted.LoadAsync("second"))!.Channels.Count);
        Assert.Null(await restarted.LoadAsync("missing"));
        using var connection = db.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Channels WHERE ContentType='vod';";
        Assert.Equal(1L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name IN ('IX_Channels_Category','IX_Channels_Name');";
        Assert.Equal(2L, command.ExecuteScalar());
    }

    [Fact]
    public async Task AtomicReplacementPreservesFavoritesAndRollsBackEveryTableOnFailure()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        var fixture = CatalogFixtures.Create(10); await db.Store.ReplaceAsync("first", fixture);
        db.Execute("INSERT INTO Favorites VALUES('first','live','000003','2026-10-17T12:00:00Z');");
        await db.Store.ReplaceAsync("first", fixture);
        Assert.Contains("000003", (await db.Store.LoadAsync("first"))!.FavoriteChannelIds!);
        db.Execute("CREATE TRIGGER FailFixture BEFORE INSERT ON Channels WHEN NEW.ProviderId='bad' BEGIN SELECT RAISE(ABORT,'fixture failure'); END;");
        var broken = new CatalogSnapshot([new("changed", "Changed")],
            [new("good", "Good", "changed", "live", "ts"), new("bad", "Bad", null, "live", "ts")], DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<SqliteException>(() => db.Store.ReplaceAsync("first", broken));

        var loaded = (await db.Store.LoadAsync("first"))!;
        Assert.Equal(10, loaded.Channels.Count);
        Assert.Equal(fixture.SyncedAt, loaded.SyncedAt);
        Assert.DoesNotContain(loaded.Categories, category => category.Id == "changed");
        Assert.Contains("000003", loaded.FavoriteChannelIds!);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.Store.ReplaceAsync("first", broken, cancellation.Token));
        Assert.Equal(10, (await db.Store.LoadAsync("first"))!.Channels.Count);
    }

    [Fact]
    public async Task FailedSecondEndpointPreservesKnownCacheAndSuccessfulEmptyCatalogReplacesIt()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        var fixture = CatalogFixtures.Create(20); await db.Store.ReplaceAsync("first", fixture);
        var service = new CatalogService(db.Store);
        using var broken = new FixtureProvider(fixture, failChannels: true);
        await Assert.ThrowsAsync<XtreamApiException>(() => service.RefreshAsync("first", broken));
        Assert.Equal(20, (await service.LoadAsync("first"))!.Channels.Count);
        using var empty = new FixtureProvider(new([], [], DateTimeOffset.UtcNow));
        Assert.Empty((await service.RefreshAsync("first", empty)).Channels);
    }

    [Fact]
    public async Task ChangedRemoteIdsRemoveOldFavoritesAndPreserveDistinctNames()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        db.Execute("INSERT INTO Favorites VALUES('first','live','000003','2026-10-17T12:00:00Z');");
        var replacement = new CatalogSnapshot([], [new("new-id", "Same", null, "live", null), new("other-id", "Same", null, "live", null)], DateTimeOffset.UtcNow);
        await db.Store.ReplaceAsync("first", replacement);
        var result = (await db.Store.LoadAsync("first"))!;
        Assert.Equal(2, result.Channels.Count);
        Assert.Empty(result.FavoriteChannelIds!);
    }

    [Fact]
    public async Task MigrationFromV1PreservesProfilesChannelsFavoritesAndHistory()
    {
        using var db = new TestDatabase();
        db.Execute("""
            CREATE TABLE SchemaVersion(Version INTEGER NOT NULL); INSERT INTO SchemaVersion VALUES(1);
            CREATE TABLE Profiles(Id TEXT PRIMARY KEY,Name TEXT,BaseUrl TEXT,CredentialKey TEXT,CreatedAt TEXT);
            CREATE TABLE Categories(ProfileId TEXT,ProviderId TEXT,Name TEXT,PRIMARY KEY(ProfileId,ProviderId));
            CREATE TABLE Channels(ProfileId TEXT,ProviderId TEXT,CategoryId TEXT,Name TEXT,StreamType TEXT,ContainerExtension TEXT,PRIMARY KEY(ProfileId,ProviderId));
            CREATE TABLE Favorites(ProfileId TEXT,ChannelId TEXT,CreatedAt TEXT,PRIMARY KEY(ProfileId,ChannelId),FOREIGN KEY(ProfileId,ChannelId) REFERENCES Channels(ProfileId,ProviderId) ON DELETE CASCADE);
            CREATE TABLE PlaybackHistory(Id INTEGER PRIMARY KEY,ProfileId TEXT,ChannelId TEXT,PlayedAt TEXT);
            INSERT INTO Profiles VALUES('first','Fixture','https://provider.invalid','fixture-key','2026-10-17T12:00:00Z');
            INSERT INTO Categories VALUES('first','001','Cat');
            INSERT INTO Channels VALUES('first','0042','001','Águila','live','ts');
            INSERT INTO Favorites VALUES('first','0042','2026-10-17T12:00:00Z');
            INSERT INTO PlaybackHistory VALUES(1,'first','0042','2026-10-17T12:00:00Z');
            """);
        await db.Settings.InitializeAsync(); await db.Settings.InitializeAsync();
        using var connection = db.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProviderId FROM Channels WHERE ProfileId='first' AND ContentType='live';";
        Assert.Equal("0042", command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM Favorites WHERE ContentType='live';"; Assert.Equal(1L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(*) FROM PlaybackHistory WHERE ContentType='live';"; Assert.Equal(1L, command.ExecuteScalar());
        command.CommandText = "PRAGMA foreign_key_check;"; Assert.Null(command.ExecuteScalar());
    }
}
