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
        var key = CatalogSearch.Normalize(query);
        var result = new List<MediaItem>();
        foreach (var (item, name) in _index)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(categoryId) && item.CategoryId != categoryId) continue;
            if (restrictedCategories?.Contains(item.CategoryId ?? "") == true) continue;
            if (name.Contains(key, StringComparison.Ordinal)) result.Add(item);
        }
        return result;
    }
}
