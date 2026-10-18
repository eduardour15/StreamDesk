using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Playback;

public static class NativeFailureClassifier
{
    public static PlaybackFailure Classify(string? module, string message)
    {
        module ??= "";
        bool Has(string value) => message.Contains(value, StringComparison.OrdinalIgnoreCase);
        var http = module.Contains("http", StringComparison.OrdinalIgnoreCase) || module.Contains("access", StringComparison.OrdinalIgnoreCase);
        if (http)
        {
            if (Has("HTTP 401") || Has("401 Unauthorized")) return PlaybackFailure.Unauthorized;
            if (Has("HTTP 403") || Has("403 Forbidden")) return PlaybackFailure.Forbidden;
            if (Has("HTTP 404") || Has("404 Not Found")) return PlaybackFailure.NotFound;
            if (Has("HTTP 408") || Has("timed out")) return PlaybackFailure.Timeout;
            if (Has("HTTP 500") || Has("HTTP 502") || Has("HTTP 503") || Has("HTTP 504") || Has("connection reset") || Has("connection refused") || Has("network is unreachable"))
                return PlaybackFailure.Network;
        }
        var hardware = module.Contains("d3d11va", StringComparison.OrdinalIgnoreCase) || module.Contains("dxva2", StringComparison.OrdinalIgnoreCase);
        if (hardware && (Has("failed") || Has("cannot") || Has("error"))) return PlaybackFailure.HardwareDecoder;
        if (module.Contains("avcodec", StringComparison.OrdinalIgnoreCase) && Has("codec not supported")) return PlaybackFailure.UnsupportedFormat;
        return PlaybackFailure.None;
    }
}
