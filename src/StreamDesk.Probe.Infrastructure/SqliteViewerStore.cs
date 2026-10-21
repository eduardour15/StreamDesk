using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SqliteViewerStore(string path, bool pooling = true) : IViewerStore
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
        { DataSource = Path.GetFullPath(path), ForeignKeys = true, Pooling = pooling, DefaultTimeout = 5 }.ToString();

    public Task<ViewerLibrary> LoadAsync(string profileId, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("$profile", profileId);
        command.CommandText = "SELECT ChannelId FROM Favorites WHERE ProfileId=$profile AND ContentType='live';";
        var favorites = new HashSet<string>(StringComparer.Ordinal);
        using (var reader = command.ExecuteReader())
            while (reader.Read()) { cancellationToken.ThrowIfCancellationRequested(); favorites.Add(reader.GetString(0)); }
        command.CommandText = "SELECT ChannelId,PlayedAt FROM PlaybackHistory WHERE ProfileId=$profile AND ContentType='live' ORDER BY PlayedAt DESC,ChannelId LIMIT 50;";
        var recents = new List<RecentChannel>();
        using (var reader = command.ExecuteReader())
            while (reader.Read()) { cancellationToken.ThrowIfCancellationRequested(); recents.Add(new(reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1)))); }
        EnsureAmplificationColumn(connection, transaction);
        command.CommandText = "SELECT AutoPlayLast, AmplificationDb FROM ProfilePreferences WHERE ProfileId=$profile;";
        var (autoPlay, amplificationDb) = ReadPreferences(command);
        cancellationToken.ThrowIfCancellationRequested(); transaction.Commit();
        return new ViewerLibrary(favorites, recents, autoPlay, amplificationDb);
    }, cancellationToken);

    public Task SetFavoriteAsync(string profileId, string contentType, string channelId, bool favorite, CancellationToken cancellationToken = default) =>
        WriteAsync(profileId, contentType, channelId, favorite ? """
            INSERT INTO Favorites(ProfileId,ContentType,ChannelId,CreatedAt)
            SELECT $profile,$type,$channel,$date WHERE EXISTS (SELECT 1 FROM Channels WHERE ProfileId=$profile AND ContentType=$type AND ProviderId=$channel)
            ON CONFLICT(ProfileId,ContentType,ChannelId) DO NOTHING;
            """ : "DELETE FROM Favorites WHERE ProfileId=$profile AND ContentType=$type AND ChannelId=$channel;", DateTimeOffset.UtcNow, cancellationToken);

    public Task RecordPlayedAsync(string profileId, string contentType, string channelId, DateTimeOffset playedAt, CancellationToken cancellationToken = default) =>
        WriteAsync(profileId, contentType, channelId, """
            INSERT INTO PlaybackHistory(ProfileId,ContentType,ChannelId,PlayedAt)
            SELECT $profile,$type,$channel,$date WHERE EXISTS (SELECT 1 FROM Channels WHERE ProfileId=$profile AND ContentType=$type AND ProviderId=$channel)
            ON CONFLICT(ProfileId,ContentType,ChannelId) DO UPDATE SET PlayedAt=MAX(PlayedAt,excluded.PlayedAt);
            DELETE FROM PlaybackHistory WHERE ProfileId=$profile AND ContentType=$type AND ChannelId NOT IN
                (SELECT ChannelId FROM PlaybackHistory WHERE ProfileId=$profile AND ContentType=$type ORDER BY PlayedAt DESC,ChannelId LIMIT 50);
            """, playedAt, cancellationToken);

    private Task WriteAsync(string profile, string type, string channel, string sql, DateTimeOffset date, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        command.Parameters.AddWithValue("$profile", profile); command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$channel", channel); command.Parameters.AddWithValue("$date", date.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery(); token.ThrowIfCancellationRequested(); transaction.Commit();
    }, token);

    public Task SetAutoPlayAsync(string profileId, bool enabled, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested(); using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ProfilePreferences(ProfileId,AutoPlayLast) VALUES($profile,$enabled) ON CONFLICT(ProfileId) DO UPDATE SET AutoPlayLast=excluded.AutoPlayLast;";
        command.Parameters.AddWithValue("$profile", profileId); command.Parameters.AddWithValue("$enabled", enabled);
        command.ExecuteNonQuery();
    }, cancellationToken);

    public Task SetAmplificationAsync(string profileId, int amplificationDb, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clamped = Math.Clamp(amplificationDb, 0, 12);
        using var connection = Open();
        EnsureAmplificationColumn(connection, transaction: null);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ProfilePreferences(ProfileId,AmplificationDb) VALUES($profile,$amp) ON CONFLICT(ProfileId) DO UPDATE SET AmplificationDb=excluded.AmplificationDb;";
        command.Parameters.AddWithValue("$profile", profileId);
        command.Parameters.AddWithValue("$amp", clamped);
        command.ExecuteNonQuery();
    }, cancellationToken);

    private static (bool AutoPlay, int AmplificationDb) ReadPreferences(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return (false, 0);
        var autoPlay = !reader.IsDBNull(0) && reader.GetInt64(0) == 1;
        var amp = reader.FieldCount > 1 && !reader.IsDBNull(1) ? Math.Clamp(reader.GetInt32(1), 0, 12) : 0;
        return (autoPlay, amp);
    }

    private static void EnsureAmplificationColumn(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='ProfilePreferences';";
        var sql = check.ExecuteScalar() as string ?? string.Empty;
        if (sql.Contains("BETWEEN 0 AND 6", StringComparison.OrdinalIgnoreCase))
        {
            using var migrate = connection.CreateCommand();
            migrate.Transaction = transaction;
            migrate.CommandText = """
                CREATE TABLE IF NOT EXISTS ProfilePreferences_new (
                    ProfileId TEXT PRIMARY KEY NOT NULL REFERENCES Profiles(Id) ON DELETE CASCADE,
                    AutoPlayLast INTEGER NOT NULL DEFAULT 0 CHECK(AutoPlayLast IN (0,1)),
                    AmplificationDb INTEGER NOT NULL DEFAULT 0 CHECK(AmplificationDb BETWEEN 0 AND 20)
                );
                INSERT INTO ProfilePreferences_new (ProfileId, AutoPlayLast, AmplificationDb)
                    SELECT ProfileId, AutoPlayLast, AmplificationDb FROM ProfilePreferences;
                DROP TABLE ProfilePreferences;
                ALTER TABLE ProfilePreferences_new RENAME TO ProfilePreferences;
                """;
            migrate.ExecuteNonQuery();
        }
        else if (!sql.Contains("AmplificationDb", StringComparison.OrdinalIgnoreCase))
        {
            using var alter = connection.CreateCommand();
            alter.Transaction = transaction;
            alter.CommandText = "ALTER TABLE ProfilePreferences ADD COLUMN AmplificationDb INTEGER NOT NULL DEFAULT 0 CHECK(AmplificationDb BETWEEN 0 AND 20);";
            alter.ExecuteNonQuery();
        }
    }

    public Task<string?> GetLastProfileAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested(); using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT LastProfileId FROM AppPreferences WHERE Id=1;"; return command.ExecuteScalar() as string;
    }, cancellationToken);

    public Task SetLastProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested(); using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO AppPreferences(Id,LastProfileId) VALUES(1,$profile) ON CONFLICT(Id) DO UPDATE SET LastProfileId=excluded.LastProfileId;";
        command.Parameters.AddWithValue("$profile", profileId); command.ExecuteNonQuery();
    }, cancellationToken);

    public Task<IReadOnlyList<SyncFavoriteItem>> GetFavoritesForSyncAsync(string profileId, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$profile", profileId);
        command.CommandText = "SELECT ContentType, ChannelId, CreatedAt FROM Favorites WHERE ProfileId=$profile;";
        var list = new List<SyncFavoriteItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            list.Add(new SyncFavoriteItem(reader.GetString(0), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2))));
        }
        return (IReadOnlyList<SyncFavoriteItem>)list;
    }, cancellationToken);

    public Task<IReadOnlyList<SyncHistoryItem>> GetHistoryForSyncAsync(string profileId, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$profile", profileId);
        command.CommandText = "SELECT ContentType, ChannelId, PlayedAt FROM PlaybackHistory WHERE ProfileId=$profile ORDER BY PlayedAt DESC;";
        var list = new List<SyncHistoryItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            list.Add(new SyncHistoryItem(reader.GetString(0), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2))));
        }
        return (IReadOnlyList<SyncHistoryItem>)list;
    }, cancellationToken);

    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }
}
