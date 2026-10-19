namespace StreamDesk.Probe.Infrastructure;

internal static class ProfileAndSettingsMigration
{
    internal const string Sql = """
        CREATE TABLE IF NOT EXISTS AppSettings (
            Id INTEGER PRIMARY KEY CHECK(Id=1),
            Theme TEXT NOT NULL DEFAULT 'Dark',
            UiScale TEXT NOT NULL DEFAULT '100%',
            Language TEXT NOT NULL DEFAULT 'es',
            ParentalControlEnabled INTEGER NOT NULL DEFAULT 0,
            ParentalPinHash TEXT,
            ParentalPinSalt TEXT
        );
        INSERT INTO AppSettings (Id, Theme, UiScale, Language, ParentalControlEnabled)
        SELECT 1, 'Dark', '100%', 'es', 0
        WHERE NOT EXISTS (SELECT 1 FROM AppSettings WHERE Id=1);
        UPDATE SchemaVersion SET Version = 5;
        """;
}
