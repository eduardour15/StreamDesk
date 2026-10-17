namespace StreamDesk.Probe.Core;

public static class ProviderEndpoint
{
    public static bool TryParse(string? value, out Uri? baseUri)
    {
        baseUri = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(parsed.Host) ||
            parsed.UserInfo.Length != 0 || parsed.Query.Length != 0 || parsed.Fragment.Length != 0) return false;
        var segments = Uri.UnescapeDataString(parsed.AbsolutePath).Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment.EndsWith(".php", StringComparison.OrdinalIgnoreCase) || segment.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || segment.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)) return false;
            if (segments.Length - index >= 4 && (segment.Equals("live", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("movie", StringComparison.OrdinalIgnoreCase) || segment.Equals("series", StringComparison.OrdinalIgnoreCase))) return false;
        }
        baseUri = parsed;
        return true;
    }
}
