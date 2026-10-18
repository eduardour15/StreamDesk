using System.Globalization;
using System.Text;
using System.Text.Json;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed partial class XtreamApiClient
{
    public async Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        if (kind is not (ContentKind.Movie or ContentKind.Series)) throw new ArgumentOutOfRangeException(nameof(kind));
        var categories = await GetJsonAsync(BuildApiUri(kind == ContentKind.Movie ? "get_vod_categories" : "get_series_categories"), false, cancellationToken).ConfigureAwait(false);
        var root = await GetJsonAsync(BuildApiUri(kind == ContentKind.Movie ? "get_vod_streams" : "get_series"), false, cancellationToken).ConfigureAwait(false);
        var items = ParseArray(root, item => ParseMedia(item, kind), item => item.StorageKey);
        if (items.Count > 50000) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        return new(ParseArray(categories, ParseCategory, item => item.Id), items, DateTimeOffset.UtcNow);
    }

    public async Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var root = await GetJsonAsync(BuildApiUri("get_vod_info", "vod_id", movie.Id), false, cancellationToken).ConfigureAwait(false);
        if (root.ValueKind != JsonValueKind.Object) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        var info = root.TryGetProperty("info", out var details) && details.ValueKind == JsonValueKind.Object ? details : root;
        var extension = movie.Extension;
        if (root.TryGetProperty("movie_data", out var data) && data.ValueKind == JsonValueKind.Object)
            extension = ReadString(data, "container_extension")?.Trim().ToLowerInvariant() ?? extension;
        return MediaMetadata.Sanitize(movie with { Plot = SafeText(ReadString(info, "plot") ?? ReadString(info, "description")), Extension = extension,
            PosterUrl = SafeLogoUrl(ReadString(info, "movie_image")) ?? movie.PosterUrl });
    }

    public async Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var root = await GetJsonAsync(BuildApiUri("get_series_info", "series_id", seriesId), false, cancellationToken).ConfigureAwait(false);
        if (root.ValueKind != JsonValueKind.Object) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        var seasons = new HashSet<int>(); var items = new List<MediaItem>(); var seen = new HashSet<string>();
        if (root.TryGetProperty("seasons", out var seasonList) && seasonList.ValueKind == JsonValueKind.Array)
            foreach (var season in seasonList.EnumerateArray())
                if (season.ValueKind == JsonValueKind.Object && ReadInt(season, "season_number") is int number && number >= 0) seasons.Add(number);
        if (root.TryGetProperty("episodes", out var episodes) && episodes.ValueKind == JsonValueKind.Object)
            foreach (var season in episodes.EnumerateObject())
            {
                if (season.Value.ValueKind != JsonValueKind.Array) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
                var seasonNumber = int.TryParse(season.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
                seasons.Add(seasonNumber);
                foreach (var episode in season.Value.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = ParseMedia(episode, ContentKind.Episode) with { SeriesId = seriesId, Season = seasonNumber, CategoryId = seasonNumber.ToString(CultureInfo.InvariantCulture) };
                    if (seen.Add(item.StorageKey)) items.Add(item);
                    if (items.Count > 20000) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
                }
            }
        return new(seasons.Order().Select(number => new XtreamCategory(number.ToString(CultureInfo.InvariantCulture), $"Temporada {number}")).ToArray(),
            items.OrderBy(item => item.Season).ThenBy(item => item.Episode).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray(), DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var root = await GetJsonAsync(BuildApiUri("get_short_epg", "stream_id", channelId), false, cancellationToken).ConfigureAwait(false);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("epg_listings", out var listings) || listings.ValueKind != JsonValueKind.Array) return [];
        var programs = new List<EpgProgram>();
        foreach (var item in listings.EnumerateArray().Take(512))
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var start = ReadUnixDate(item, "start_timestamp"); var end = ReadUnixDate(item, "stop_timestamp") ?? ReadUnixDate(item, "end_timestamp");
            var title = SafeText(DecodeText(ReadString(item, "title")));
            if (start.HasValue && end > start && title.Length > 0)
                programs.Add(new(title, start.Value, end!.Value, SafeText(DecodeText(ReadString(item, "description")))));
        }
        return programs.DistinctBy(item => (item.StartUtc, item.EndUtc, item.Title)).OrderBy(item => item.StartUtc).ToArray();
    }

    public Uri CreateMediaUri(MediaItem item)
    {
        EnsureAuthenticated();
        if (item.Kind == ContentKind.Series || string.IsNullOrWhiteSpace(item.Id) || !MediaMetadata.SafeExtension(item.Extension))
            throw new XtreamApiException(XtreamFailureKind.FormatNotSupported);
        var path = AppendPath(_baseUri!.AbsolutePath, $"{(item.Kind == ContentKind.Movie ? "movie" : "series")}/{EscapePathSegment(_username!)}/{EscapePathSegment(_password!)}/{EscapePathSegment(item.Id)}.{item.Extension}");
        return new Uri(_baseUri.GetLeftPart(UriPartial.Authority) + path, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });
    }

    private MediaItem ParseMedia(JsonElement item, ContentKind kind)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        var id = ReadString(item, kind == ContentKind.Series ? "series_id" : kind == ContentKind.Episode ? "id" : "stream_id");
        var name = SafeText(ReadString(item, "name") ?? ReadString(item, "title"));
        if (string.IsNullOrWhiteSpace(id) || name.Length == 0) throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        return MediaMetadata.Sanitize(new(id.Trim(), name, kind, ReadString(item, "category_id"),
            ReadString(item, "container_extension")?.Trim().TrimStart('.').ToLowerInvariant(), SafeLogoUrl(ReadString(item, "stream_icon") ?? ReadString(item, "cover")),
            SafeText(ReadString(item, "plot")), Episode: ReadInt(item, "episode_num") ?? 0));
    }
    private string SafeText(string? value) => MediaMetadata.Text(value).Replace(_username!, "[oculto]", StringComparison.Ordinal).Replace(_password!, "[oculto]", StringComparison.Ordinal);
    private static string? DecodeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 16384) return null;
        try { var decoded = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(value)); return decoded.Any(char.IsControl) ? value : decoded; }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException) { return value; }
    }
}
