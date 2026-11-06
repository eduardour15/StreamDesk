namespace StreamDesk.Probe.Infrastructure;

internal static class MediaFavoritesMigration
{
    internal const string Sql = """
        CREATE TABLE IF NOT EXISTS MediaFavorites (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ContentType TEXT NOT NULL CHECK(ContentType IN ('movie', 'series')),
            ContentId TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            PRIMARY KEY(ProfileId, ContentType, ContentId)
        );
        CREATE INDEX IF NOT EXISTS IX_MediaFavorites_Profile ON MediaFavorites(ProfileId, ContentType);
        UPDATE SchemaVersion SET Version = 6;
        """;
}
