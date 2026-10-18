using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SqliteSettingsService : ISettingsService
{
    private const int CurrentSchemaVersion = 3;
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

    public Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default) =>
        Task.Run(() => DeleteProfileCoreAsync(profileId, cancellationToken), cancellationToken);

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
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Profile>> GetProfilesCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, BaseUrl, CredentialKey, CreatedAt FROM Profiles ORDER BY Name COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var profiles = new List<Profile>();
        while (await reader.ReadAsync(cancellationToken))
            profiles.Add(new Profile(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4))));
        return profiles;
    }

    private async Task AddProfileCoreAsync(Profile profile, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Profiles (Id, Name, BaseUrl, CredentialKey, CreatedAt) VALUES ($id, $name, $url, $key, $created);";
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$url", profile.BaseUrl);
        command.Parameters.AddWithValue("$key", profile.CredentialKey);
        command.Parameters.AddWithValue("$created", profile.CreatedAt.ToString("O"));
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
}
