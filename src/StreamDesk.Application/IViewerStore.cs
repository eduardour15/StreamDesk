using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public interface IViewerStore
{
    Task<ViewerLibrary> LoadAsync(string profileId, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(string profileId, string contentType, string channelId, bool favorite, CancellationToken cancellationToken = default);
    Task RecordPlayedAsync(string profileId, string contentType, string channelId, DateTimeOffset playedAt, CancellationToken cancellationToken = default);
    Task SetAutoPlayAsync(string profileId, bool enabled, CancellationToken cancellationToken = default);
    Task SetAmplificationAsync(string profileId, int amplificationDb, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<string?> GetLastProfileAsync(CancellationToken cancellationToken = default);
    Task SetLastProfileAsync(string profileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncFavoriteItem>> GetFavoritesForSyncAsync(string profileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncHistoryItem>> GetHistoryForSyncAsync(string profileId, CancellationToken cancellationToken = default);
}
