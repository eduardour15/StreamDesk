using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class XmlTvReader : StreamDesk.Application.IEpgImporter
{
    public Task<IReadOnlyList<EpgProgram>> ReadAsync(Stream input, string epgId, CancellationToken cancellationToken = default) => ReadChannelAsync(input, epgId, cancellationToken);
    public static async Task<IReadOnlyList<EpgProgram>> ReadChannelAsync(Stream input, string epgId, CancellationToken token = default)
    {
        using var reader = XmlReader.Create(input, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        var result = new List<EpgProgram>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "programme") continue;
            if (reader.GetAttribute("channel") != epgId) continue;
            var start = Time(reader.GetAttribute("start")); var end = Time(reader.GetAttribute("stop"));
            using var subtree = reader.ReadSubtree();
            var element = await XElement.LoadAsync(subtree, LoadOptions.None, token).ConfigureAwait(false);
            var title = MediaMetadata.Text(element.Element("title")?.Value, 512);
            if (start.HasValue && end > start && title.Length > 0) result.Add(new(title, start.Value, end!.Value, MediaMetadata.Text(element.Element("desc")?.Value)));
            if (result.Count > 512) throw new XmlException("Límite de programas superado.");
        }
        return result.DistinctBy(item => (item.StartUtc, item.EndUtc, item.Title)).OrderBy(item => item.StartUtc).ToArray();
    }
    private static DateTimeOffset? Time(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (text.Length == 14) text += " +0000";
        if (text.Length != 20 || text[14] != ' ') return null;
        text = text.Insert(18, ":");
        return DateTimeOffset.TryParseExact(text, "yyyyMMddHHmmss zzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value.ToUniversalTime() : null;
    }
}
