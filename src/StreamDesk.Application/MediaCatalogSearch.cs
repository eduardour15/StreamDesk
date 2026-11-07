using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

/// <summary>One normalized index per loaded catalog, shared by successive local queries.</summary>
public sealed class MediaCatalogSearch
{
    private readonly (MediaItem Item, string Key)[] _index;
    public MediaCatalog Catalog { get; }

    public MediaCatalogSearch(MediaCatalog catalog, CancellationToken cancellationToken = default)
    {
        Catalog = catalog;
        _index = new (MediaItem, string)[catalog.Items.Count];
        for (var i = 0; i < _index.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = catalog.Items[i];
            _index[i] = (item, CatalogSearch.Normalize(item.Name));
        }
    }

    public IReadOnlyList<MediaItem> Filter(string query, string? categoryId, IReadOnlySet<string>? restrictedCategories,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CatalogSearch.Normalize(query);
        var hasCategory = !string.IsNullOrEmpty(categoryId);
        var hasRestricted = restrictedCategories is { Count: > 0 };
        var hasQuery = key.Length > 0;
        var result = new List<MediaItem>(!hasCategory && !hasRestricted && !hasQuery ? _index.Length : Math.Min(_index.Length, 256));
        foreach (var (item, name) in _index)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasCategory && item.CategoryId != categoryId) continue;
            if (hasRestricted && restrictedCategories!.Contains(item.CategoryId ?? "")) continue;
            if (!hasQuery || name.Contains(key, StringComparison.Ordinal)) result.Add(item);
        }
        return result;
    }
}
