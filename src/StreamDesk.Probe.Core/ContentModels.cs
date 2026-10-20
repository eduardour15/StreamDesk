using System.Text.RegularExpressions;

namespace StreamDesk.Probe.Core;

public enum ContentKind { Movie, Series, Episode }
public sealed record MediaItem(string Id, string Name, ContentKind Kind, string? CategoryId = null,
    string? Extension = null, string? PosterUrl = null, string? Plot = null, string? SeriesId = null, int Season = 0, int Episode = 0)
{
    public string StorageKey => Kind == ContentKind.Episode ? $"{SeriesId?.Length ?? 0}:{SeriesId}:{Season}:{Id}" : Id;
    public string Description => string.IsNullOrWhiteSpace(Plot) ? "Sin sinopsis disponible." : Plot;
}
public sealed record MediaCatalog(IReadOnlyList<XtreamCategory> Categories, IReadOnlyList<MediaItem> Items, DateTimeOffset SyncedAt);
public sealed record MediaProgress(MediaItem Item, long PositionMilliseconds, long DurationMilliseconds, DateTimeOffset UpdatedAt, bool Completed = false)
{
    public string Label => $"{Item.Name} · {TimeSpan.FromMilliseconds(PositionMilliseconds):hh\\:mm\\:ss}";
}
public sealed record EpgProgram(string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Description = null)
{
    public string Label => $"{StartUtc.LocalDateTime:t}–{EndUtc.LocalDateTime:t} · {Title}";
}
public sealed record MediaTrack(int Id, string Name);
public sealed record MediaPlaybackDetails(long TimeMilliseconds, long LengthMilliseconds, bool CanSeek,
    IReadOnlyList<MediaTrack> AudioTracks, IReadOnlyList<MediaTrack> SubtitleTracks, int AudioTrack = -1, int SubtitleTrack = -1)
{
    public static MediaPlaybackDetails Empty { get; } = new(0, 0, false, [], []);
}
public static class MediaMetadata
{
    public static string Text(string? text, int limit = 8192)
    {
        text = (text ?? "")[..Math.Min(text?.Length ?? 0, limit)];
        return Regex.Replace(text, @"https?://\S+", "[enlace omitido]", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)).Trim();
    }
    public static bool SafeImage(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0
        && (uri.Query.Length == 0 || !HasSensitiveQueryParameter(uri.Query));

    private static bool HasSensitiveQueryParameter(string query)
    {
        var raw = query.TrimStart('?');
        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eq >= 0 ? pair[..eq] : pair).Trim();
            if (key.Equals("password", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("pass", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("username", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("token", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("access_token", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("api_key", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("apikey", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("auth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
    public static bool SafeExtension(string? value) => value is "mp4" or "mkv" or "avi" or "mov" or "m4v" or "ts" or "m3u8" or "webm" or "mpg" or "mpeg";
    public static bool Identifier([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && !value.Contains("://", StringComparison.Ordinal) && !value.Any(char.IsControl);
    public static MediaItem Sanitize(MediaItem item)
    {
        if (!Identifier(item.Id) || !Enum.IsDefined(item.Kind) || (item.Kind == ContentKind.Episode && !Identifier(item.SeriesId))) throw new ArgumentException("Identificador de contenido no válido.");
        return item with { Name = Text(item.Name, 512), Plot = Text(item.Plot), PosterUrl = SafeImage(item.PosterUrl) ? item.PosterUrl : null,
            Extension = SafeExtension(item.Extension) ? item.Extension : null };
    }
}
