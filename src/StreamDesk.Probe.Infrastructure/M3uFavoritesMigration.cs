namespace StreamDesk.Probe.Infrastructure;

internal static class M3uFavoritesMigration
{
    internal const string Sql = """
        CREATE TABLE IF NOT EXISTS M3uChannelFavorites (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ChannelId TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            PRIMARY KEY(ProfileId, ChannelId)
        );
        CREATE INDEX IF NOT EXISTS IX_M3uChannelFavorites_Profile ON M3uChannelFavorites(ProfileId);
        UPDATE SchemaVersion SET Version = 7;
        """;
}
