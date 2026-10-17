using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class SqliteCatalogStore : ICatalogStore
{
    private readonly string _connectionString;
    public SqliteCatalogStore(string path, bool pooling = true) =>
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), ForeignKeys = true, Pooling = pooling, DefaultTimeout = 5 }.ToString();

    public Task<CatalogSnapshot?> LoadAsync(string profileId, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(profileId, cancellationToken), cancellationToken);

    private CatalogSnapshot? Load(string profileId, CancellationToken token)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$profile", profileId);
        command.CommandText = "SELECT SyncedAt FROM CatalogSync WHERE ProfileId=$profile AND ContentType='live';";
        var synced = command.ExecuteScalar() as string;
        if (synced is null) return null;
        var categories = new List<XtreamCategory>();
        command.CommandText = "SELECT ProviderId,Name FROM Categories WHERE ProfileId=$profile AND ContentType='live' ORDER BY Name,ProviderId;";
        using (var reader = command.ExecuteReader())
            while (reader.Read()) { token.ThrowIfCancellationRequested(); categories.Add(new(reader.GetString(0), reader.GetString(1))); }
        var channels = new List<XtreamChannel>();
        command.CommandText = "SELECT ProviderId,Name,CategoryId,StreamType,ContainerExtension,LogoUrl,EpgChannelId FROM Channels WHERE ProfileId=$profile AND ContentType='live' ORDER BY NormalizedName,ProviderId;";
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                string? Optional(int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
                channels.Add(new(reader.GetString(0), reader.GetString(1), Optional(2), Optional(3), Optional(4), Optional(5), Optional(6)));
            }
        var favorites = new HashSet<string>(StringComparer.Ordinal);
        command.CommandText = "SELECT ChannelId FROM Favorites WHERE ProfileId=$profile AND ContentType='live';";
        using (var reader = command.ExecuteReader())
            while (reader.Read()) { token.ThrowIfCancellationRequested(); favorites.Add(reader.GetString(0)); }
        transaction.Commit();
        return new(categories, channels, DateTimeOffset.Parse(synced), favorites);
    }

    public Task ReplaceAsync(string profileId, CatalogSnapshot catalog, CancellationToken cancellationToken = default) =>
        Task.Run(() => Replace(profileId, catalog, cancellationToken), cancellationToken);

    private void Replace(string profileId, CatalogSnapshot catalog, CancellationToken token)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "CREATE TEMP TABLE IncomingChannels(Id TEXT PRIMARY KEY); CREATE TEMP TABLE IncomingCategories(Id TEXT PRIMARY KEY);";
        command.ExecuteNonQuery();
        command.Parameters.AddWithValue("$profile", profileId);
        command.Parameters.AddWithValue("$id", "");
        command.Parameters.AddWithValue("$name", "");
        command.Parameters.AddWithValue("$category", DBNull.Value);
        command.Parameters.AddWithValue("$normalized", "");
        command.Parameters.AddWithValue("$streamType", DBNull.Value);
        command.Parameters.AddWithValue("$extension", DBNull.Value);
        command.Parameters.AddWithValue("$logo", DBNull.Value);
        command.Parameters.AddWithValue("$epg", DBNull.Value);
        foreach (var category in catalog.Categories.DistinctBy(item => item.Id))
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(category.Id) || string.IsNullOrWhiteSpace(category.Name)) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
            command.CommandText = """
                INSERT INTO IncomingCategories VALUES($id);
                INSERT INTO Categories(ProfileId,ContentType,ProviderId,Name) VALUES($profile,'live',$id,$name)
                ON CONFLICT(ProfileId,ContentType,ProviderId) DO UPDATE SET Name=excluded.Name;
                """;
            command.Parameters["$id"].Value = category.Id;
            command.Parameters["$name"].Value = category.Name;
            command.ExecuteNonQuery();
        }
        foreach (var channel in catalog.Channels.DistinctBy(item => item.Id))
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(channel.Id) || string.IsNullOrWhiteSpace(channel.Name)) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
            command.CommandText = """
                INSERT INTO IncomingChannels VALUES($id);
                INSERT INTO Channels(ProfileId,ContentType,ProviderId,Name,NormalizedName,CategoryId,StreamType,ContainerExtension,LogoUrl,EpgChannelId)
                VALUES($profile,'live',$id,$name,$normalized,$category,$streamType,$extension,$logo,$epg)
                ON CONFLICT(ProfileId,ContentType,ProviderId) DO UPDATE SET
                    Name=excluded.Name,NormalizedName=excluded.NormalizedName,CategoryId=excluded.CategoryId,
                    StreamType=excluded.StreamType,ContainerExtension=excluded.ContainerExtension,LogoUrl=excluded.LogoUrl,EpgChannelId=excluded.EpgChannelId;
                """;
            command.Parameters["$id"].Value = channel.Id;
            command.Parameters["$name"].Value = channel.Name;
            command.Parameters["$normalized"].Value = CatalogSearch.Normalize(channel.Name);
            command.Parameters["$category"].Value = (object?)channel.CategoryId ?? DBNull.Value;
            command.Parameters["$streamType"].Value = (object?)channel.StreamType ?? DBNull.Value;
            command.Parameters["$extension"].Value = (object?)channel.ContainerExtension ?? DBNull.Value;
            command.Parameters["$logo"].Value = IsSafeLogo(channel.LogoUrl) ? channel.LogoUrl! : DBNull.Value;
            command.Parameters["$epg"].Value = (object?)channel.EpgChannelId ?? DBNull.Value;
            command.ExecuteNonQuery();
        }
        token.ThrowIfCancellationRequested();
        command.CommandText = """
            DELETE FROM Channels WHERE ProfileId=$profile AND ContentType='live' AND ProviderId NOT IN (SELECT Id FROM IncomingChannels);
            DELETE FROM Categories WHERE ProfileId=$profile AND ContentType='live' AND ProviderId NOT IN (SELECT Id FROM IncomingCategories);
            INSERT INTO CatalogSync(ProfileId,ContentType,SyncedAt) VALUES($profile,'live',$synced)
            ON CONFLICT(ProfileId,ContentType) DO UPDATE SET SyncedAt=excluded.SyncedAt;
            DROP TABLE IncomingChannels;
            DROP TABLE IncomingCategories;
            """;
        command.Parameters.AddWithValue("$synced", catalog.SyncedAt.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
        token.ThrowIfCancellationRequested();
        transaction.Commit();
    }

    private static bool IsSafeLogo(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && uri.Query.Length == 0 && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
