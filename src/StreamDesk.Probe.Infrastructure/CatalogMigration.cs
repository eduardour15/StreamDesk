namespace StreamDesk.Probe.Infrastructure;

internal static class CatalogMigration
{
    internal const string Sql = """
        CREATE TABLE CategoriesV2 (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ContentType TEXT NOT NULL, ProviderId TEXT NOT NULL, Name TEXT NOT NULL,
            PRIMARY KEY(ProfileId, ContentType, ProviderId));
        INSERT INTO CategoriesV2 SELECT ProfileId, 'live', ProviderId, Name FROM Categories;
        CREATE TABLE ChannelsV2 (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ContentType TEXT NOT NULL, ProviderId TEXT NOT NULL, CategoryId TEXT,
            Name TEXT NOT NULL, NormalizedName TEXT NOT NULL, StreamType TEXT,
            ContainerExtension TEXT, LogoUrl TEXT, EpgChannelId TEXT,
            PRIMARY KEY(ProfileId, ContentType, ProviderId));
        INSERT INTO ChannelsV2
            SELECT ProfileId, 'live', ProviderId, CategoryId, Name, UPPER(Name), StreamType, ContainerExtension, NULL, NULL FROM Channels;
        CREATE TABLE FavoritesV2 (
            ProfileId TEXT NOT NULL, ContentType TEXT NOT NULL, ChannelId TEXT NOT NULL, CreatedAt TEXT NOT NULL,
            PRIMARY KEY(ProfileId, ContentType, ChannelId),
            FOREIGN KEY(ProfileId, ContentType, ChannelId) REFERENCES ChannelsV2(ProfileId, ContentType, ProviderId) ON DELETE CASCADE);
        INSERT INTO FavoritesV2 SELECT ProfileId, 'live', ChannelId, CreatedAt FROM Favorites;
        CREATE TABLE PlaybackHistoryV2 (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ContentType TEXT NOT NULL, ChannelId TEXT NOT NULL, PlayedAt TEXT NOT NULL);
        INSERT INTO PlaybackHistoryV2 SELECT Id, ProfileId, 'live', ChannelId, PlayedAt FROM PlaybackHistory;
        DROP TABLE Favorites;
        DROP TABLE PlaybackHistory;
        DROP TABLE Channels;
        DROP TABLE Categories;
        ALTER TABLE CategoriesV2 RENAME TO Categories;
        ALTER TABLE ChannelsV2 RENAME TO Channels;
        ALTER TABLE FavoritesV2 RENAME TO Favorites;
        ALTER TABLE PlaybackHistoryV2 RENAME TO PlaybackHistory;
        CREATE TABLE CatalogSync (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ContentType TEXT NOT NULL, SyncedAt TEXT NOT NULL,
            PRIMARY KEY(ProfileId, ContentType));
        CREATE INDEX IX_Channels_Category ON Channels(ProfileId, ContentType, CategoryId);
        CREATE INDEX IX_Channels_Name ON Channels(ProfileId, ContentType, NormalizedName);
        UPDATE SchemaVersion SET Version = 2;
        """;
}
