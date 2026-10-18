namespace StreamDesk.Probe.Infrastructure;

internal static class ViewerMigration
{
    internal const string Sql = """
        CREATE TABLE PlaybackHistoryV3 (
            ProfileId TEXT NOT NULL, ContentType TEXT NOT NULL, ChannelId TEXT NOT NULL, PlayedAt TEXT NOT NULL,
            PRIMARY KEY(ProfileId,ContentType,ChannelId),
            FOREIGN KEY(ProfileId,ContentType,ChannelId) REFERENCES Channels(ProfileId,ContentType,ProviderId) ON DELETE CASCADE);
        INSERT INTO PlaybackHistoryV3
            SELECT h.ProfileId,h.ContentType,h.ChannelId,MAX(h.PlayedAt) FROM PlaybackHistory h
            JOIN Channels c ON c.ProfileId=h.ProfileId AND c.ContentType=h.ContentType AND c.ProviderId=h.ChannelId
            GROUP BY h.ProfileId,h.ContentType,h.ChannelId;
        DROP TABLE PlaybackHistory;
        ALTER TABLE PlaybackHistoryV3 RENAME TO PlaybackHistory;
        CREATE INDEX IX_History_Recent ON PlaybackHistory(ProfileId,ContentType,PlayedAt DESC,ChannelId);
        DELETE FROM PlaybackHistory AS h WHERE ChannelId NOT IN (
            SELECT r.ChannelId FROM PlaybackHistory r WHERE r.ProfileId=h.ProfileId AND r.ContentType=h.ContentType
            ORDER BY r.PlayedAt DESC,r.ChannelId LIMIT 50);
        CREATE TABLE ProfilePreferences (
            ProfileId TEXT PRIMARY KEY NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            AutoPlayLast INTEGER NOT NULL DEFAULT 0 CHECK(AutoPlayLast IN (0,1)));
        CREATE TABLE AppPreferences (
            Id INTEGER PRIMARY KEY CHECK(Id=1), LastProfileId TEXT REFERENCES Profiles(Id) ON DELETE SET NULL);
        UPDATE SchemaVersion SET Version=3;
        """;
}
