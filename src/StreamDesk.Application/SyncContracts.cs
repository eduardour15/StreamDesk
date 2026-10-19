namespace StreamDesk.Application;

public sealed record SyncFavoriteItem(string ContentType, string ChannelId, DateTimeOffset CreatedAt);

public sealed record SyncHistoryItem(string ContentType, string ChannelId, DateTimeOffset PlayedAt);

public sealed record SyncPackagePayload(
    int Version,
    string ExportedAt,
    string DeviceId,
    IReadOnlyList<SyncFavoriteItem> Favorites,
    IReadOnlyList<SyncHistoryItem> History);

public sealed record SyncResult(int FavoritesAdded, int HistoryMerged, string Message);

public interface ISyncService
{
    Task<int> ExportAsync(string targetFilePath, string passphrase, string profileId, CancellationToken cancellationToken = default);
    Task<SyncResult> ImportAsync(string sourceFilePath, string passphrase, string profileId, CancellationToken cancellationToken = default);
}
