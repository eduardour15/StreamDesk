using System.Globalization;
using System.Text;
using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed class CatalogSearch
{
    private readonly (XtreamChannel Channel, string Key)[] _index;

    public CatalogSearch(IReadOnlyList<XtreamChannel> channels) =>
        _index = channels.Select(channel => (channel, Normalize(channel.Name))).ToArray();

    public IReadOnlyList<XtreamChannel> Filter(string query, string? categoryId, bool favoritesOnly,
        IReadOnlySet<string>? favorites, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Normalize(query);
        var hasCategory = !string.IsNullOrEmpty(categoryId);
        var hasQuery = key.Length > 0;
        var result = new List<XtreamChannel>(!hasCategory && !favoritesOnly && !hasQuery ? _index.Length : Math.Min(_index.Length, 256));
        foreach (var entry in _index)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasCategory && entry.Channel.CategoryId != categoryId) continue;
            if (favoritesOnly && favorites?.Contains(entry.Channel.Id) != true) continue;
            if (!hasQuery || entry.Key.Contains(key, StringComparison.Ordinal)) result.Add(entry.Channel);
        }
        return result;
    }

    public static string Normalize(string value)
    {
        var text = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(text.Length);
        foreach (var character in text)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToUpperInvariant(character));
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
