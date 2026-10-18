namespace StreamDesk.Probe.Infrastructure;

internal static class ContentMigration
{
    internal const string Sql = """
        CREATE TABLE MediaCache (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            Kind INTEGER NOT NULL, Scope TEXT NOT NULL, UpdatedAt TEXT NOT NULL, Payload TEXT NOT NULL,
            PRIMARY KEY(ProfileId,Kind,Scope));
        CREATE TABLE MediaProgress (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            Kind INTEGER NOT NULL, ItemKey TEXT NOT NULL, UpdatedAt TEXT NOT NULL, Payload TEXT NOT NULL,
            PRIMARY KEY(ProfileId,Kind,ItemKey));
        CREATE TABLE EpgCache (
            ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
            ChannelId TEXT NOT NULL, UpdatedAt TEXT NOT NULL, Payload TEXT NOT NULL,
            PRIMARY KEY(ProfileId,ChannelId));
        CREATE INDEX IX_MediaProgress_Recent ON MediaProgress(ProfileId,UpdatedAt DESC);
        UPDATE SchemaVersion SET Version=4;
        """;
}
