using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public interface ICatalogStore
{
    Task<CatalogSnapshot?> LoadAsync(string profileId, CancellationToken cancellationToken = default);
    Task ReplaceAsync(string profileId, CatalogSnapshot catalog, CancellationToken cancellationToken = default);
}

public sealed class CatalogService(ICatalogStore store)
{
    public Task<CatalogSnapshot?> LoadAsync(string profileId, CancellationToken cancellationToken = default) =>
        store.LoadAsync(profileId, cancellationToken);

    public async Task<CatalogSnapshot> RefreshAsync(string profileId, IIptvProvider provider, CancellationToken cancellationToken = default)
    {
        // The API provides full lists. Only commit after BOTH endpoints succeeded.
        var categories = await provider.GetLiveCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var channels = await provider.GetLiveChannelsAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = new CatalogSnapshot(categories, channels, DateTimeOffset.UtcNow);
        await store.ReplaceAsync(profileId, snapshot, cancellationToken).ConfigureAwait(false);
        return (await store.LoadAsync(profileId, cancellationToken).ConfigureAwait(false))!;
    }
}
