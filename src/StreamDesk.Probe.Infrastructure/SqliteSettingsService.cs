using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SqliteSettingsService : ISettingsService
{
    private const int CurrentSchemaVersion = 5;
    private readonly string _connectionString;

    public SqliteSettingsService(string databasePath, bool pooling = true)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath, ForeignKeys = true, Pooling = pooling, DefaultTimeout = 5 }.ToString();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => InitializeCoreAsync(cancellationToken), cancellationToken);

    public Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => GetProfilesCoreAsync(cancellationToken), cancellationToken);

    public Task AddProfileAsync(Profile profile, CancellationToken cancellationToken = default) =>
        Task.Run(() => AddProfileCoreAsync(profile, cancellationToken), cancellationToken);

    public Task UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default) =>
        Task.Run(() => UpdateProfileCoreAsync(profile, cancellationToken), cancellationToken);

    public Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default) =>
        Task.Run(() => DeleteProfileCoreAsync(profileId, cancellationToken), cancellationToken);

    public Task<AppSettings> GetAppSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => GetAppSettingsCoreAsync(cancellationToken), cancellationToken);

    public Task SaveAppSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveAppSettingsCoreAsync(settings, cancellationToken), cancellationToken);

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS SchemaVersion (Version INTEGER NOT NULL);
            INSERT INTO SchemaVersion (Version) SELECT 0 WHERE NOT EXISTS (SELECT 1 FROM SchemaVersion);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT Version FROM SchemaVersion LIMIT 1;";
        var version = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException("La base de datos fue creada por una versión más reciente de StreamDesk.");
        if (version == 0)
        {
            command.CommandText = """
                CREATE TABLE Profiles (
                    Id TEXT PRIMARY KEY NOT NULL,
                    Name TEXT NOT NULL CHECK(length(Name) BETWEEN 1 AND 80),
                    BaseUrl TEXT NOT NULL,
                    CredentialKey TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL
                );
                CREATE TABLE Categories (
                    ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
                    ProviderId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    PRIMARY KEY(ProfileId, ProviderId)
                );
                CREATE TABLE Channels (
                    ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
                    ProviderId TEXT NOT NULL,
                    CategoryId TEXT,
                    Name TEXT NOT NULL,
                    StreamType TEXT,
                    ContainerExtension TEXT,
                    PRIMARY KEY(ProfileId, ProviderId)
                );
                CREATE TABLE Favorites (
                    ProfileId TEXT NOT NULL,
                    ChannelId TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    PRIMARY KEY(ProfileId, ChannelId),
                    FOREIGN KEY(ProfileId, ChannelId) REFERENCES Channels(ProfileId, ProviderId) ON DELETE CASCADE
                );
                CREATE TABLE PlaybackHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProfileId TEXT NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
                    ChannelId TEXT NOT NULL,
                    PlayedAt TEXT NOT NULL
                );
                UPDATE SchemaVersion SET Version = 1;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            version = 1;
        }
        if (version == 1)
        {
            command.CommandText = CatalogMigration.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            version = 2;
        }
        if (version == 2)
        {
            command.CommandText = ViewerMigration.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            version = 3;
        }
        if (version == 3)
        {
            command.CommandText = ContentMigration.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            version = 4;
        }
        if (version == 4)
        {
            var hasProviderType = false;
            command.CommandText = "PRAGMA table_info(Profiles);";
            await using (var pragmaReader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await pragmaReader.ReadAsync(cancellationToken))
                {
                    if (string.Equals(pragmaReader.GetString(1), "ProviderType", StringComparison.OrdinalIgnoreCase))
                    {
                        hasProviderType = true;
                        break;
                    }
                }
            }

            if (!hasProviderType)
            {
                command.CommandText = "ALTER TABLE Profiles ADD COLUMN ProviderType TEXT NOT NULL DEFAULT 'xtream';";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            command.CommandText = ProfileAndSettingsMigration.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await EnsureAmplificationColumnAsync(command, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureAmplificationColumnAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var hasAmplificationDb = false;
        command.CommandText = "PRAGMA table_info(ProfilePreferences);";
        await using (var prefReader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await prefReader.ReadAsync(cancellationToken))
            {
                if (string.Equals(prefReader.GetString(1), "AmplificationDb", StringComparison.OrdinalIgnoreCase))
                {
                    hasAmplificationDb = true;
                    break;
                }
            }
        }

        if (!hasAmplificationDb)
        {
            command.CommandText = "ALTER TABLE ProfilePreferences ADD COLUMN AmplificationDb INTEGER NOT NULL DEFAULT 0 CHECK(AmplificationDb BETWEEN 0 AND 6);";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyList<Profile>> GetProfilesCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, BaseUrl, CredentialKey, CreatedAt, ProviderType FROM Profiles ORDER BY Name COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var profiles = new List<Profile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var providerType = reader.FieldCount > 5 && !reader.IsDBNull(5) ? reader.GetString(5) : "xtream";
            profiles.Add(new Profile(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                providerType));
        }
        return profiles;
    }

    private async Task AddProfileCoreAsync(Profile profile, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Profiles (Id, Name, BaseUrl, CredentialKey, CreatedAt, ProviderType) VALUES ($id, $name, $url, $key, $created, $type);";
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$url", profile.BaseUrl);
        command.Parameters.AddWithValue("$key", profile.CredentialKey);
        command.Parameters.AddWithValue("$created", profile.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$type", profile.ProviderType ?? "xtream");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateProfileCoreAsync(Profile profile, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Profiles SET Name = $name, BaseUrl = $url, ProviderType = $type WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$url", profile.BaseUrl);
        command.Parameters.AddWithValue("$type", profile.ProviderType ?? "xtream");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task DeleteProfileCoreAsync(string profileId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Profiles WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", profileId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<AppSettings> GetAppSettingsCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Theme, UiScale, Language, ParentalControlEnabled, ParentalPinHash, ParentalPinSalt FROM AppSettings WHERE Id = 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new AppSettings(
                Theme: reader.GetString(0),
                UiScale: reader.GetString(1),
                Language: reader.GetString(2),
                ParentalControlEnabled: reader.GetInt32(3) == 1,
                ParentalPinHash: reader.IsDBNull(4) ? null : reader.GetString(4),
                ParentalPinSalt: reader.IsDBNull(5) ? null : reader.GetString(5));
        }
        return new AppSettings();
    }

    private async Task SaveAppSettingsCoreAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Id, Theme, UiScale, Language, ParentalControlEnabled, ParentalPinHash, ParentalPinSalt)
            VALUES (1, $theme, $scale, $lang, $parental, $pinHash, $pinSalt)
            ON CONFLICT(Id) DO UPDATE SET
                Theme = excluded.Theme,
                UiScale = excluded.UiScale,
                Language = excluded.Language,
                ParentalControlEnabled = excluded.ParentalControlEnabled,
                ParentalPinHash = excluded.ParentalPinHash,
                ParentalPinSalt = excluded.ParentalPinSalt;
            """;
        command.Parameters.AddWithValue("$theme", settings.Theme);
        command.Parameters.AddWithValue("$scale", settings.UiScale);
        command.Parameters.AddWithValue("$lang", settings.Language);
        command.Parameters.AddWithValue("$parental", settings.ParentalControlEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$pinHash", (object?)settings.ParentalPinHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$pinSalt", (object?)settings.ParentalPinSalt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
