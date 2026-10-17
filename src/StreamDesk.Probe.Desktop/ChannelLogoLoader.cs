using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace StreamDesk.Probe.Desktop;

public sealed class ChannelLogoLoader(HttpClient client) : IDisposable
{
    private readonly SemaphoreSlim _slots = new(4, 4);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _sync = new();
    private readonly Dictionary<string, (BitmapSource? Image, LinkedListNode<string> Node)> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    public int CachedCount { get { lock (_sync) return _cache.Count; } }

    public async Task<BitmapSource?> GetAsync(string? url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") ||
            uri.Query.Length != 0 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return null;
        if (TryGet(url!, out var cached)) return cached;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var entered = false;
        BitmapSource? image = null;
        try
        {
            await _slots.WaitAsync(timeout.Token); entered = true;
            if (TryGet(url!, out cached)) return cached;
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 512 * 1024) return Remember(url!, null);
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(bytes, timeout.Token)) != 0)
            {
                if (buffer.Length + read > 512 * 1024) return Remember(url!, null);
                buffer.Write(bytes, 0, read);
            }
            buffer.Position = 0;
            var encoded = buffer.ToArray();
            // WPF's built-in WIC codecs do not handle SVG, WebP, AVIF or HTML
            // error pages.  Check the payload before invoking BitmapImage so a
            // provider's unsupported logo does not produce a first-chance WIC
            // exception for every realized row.
            if (!LooksLikeWpfRasterImage(encoded)) return Remember(url!, null);
            image = await Task.Run(() =>
            {
                using var stream = new MemoryStream(encoded);
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 64; bitmap.DecodePixelHeight = 64;
                bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                return bitmap;
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _shutdown.IsCancellationRequested) { throw; }
        catch { /* An unavailable or invalid logo must not interrupt catalogue navigation. */ }
        finally { if (entered) _slots.Release(); }
        return Remember(url!, image);
    }

    private static bool LooksLikeWpfRasterImage(ReadOnlySpan<byte> data)
    {
        // PNG
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return true;
        // JPEG
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return true;
        // GIF
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8))) return true;
        // BMP
        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M') return true;
        // TIFF (little- and big-endian)
        if (data.Length >= 4 && ((data[0] == (byte)'I' && data[1] == (byte)'I' && data[2] == 0x2A && data[3] == 0x00) ||
                                 (data[0] == (byte)'M' && data[1] == (byte)'M' && data[2] == 0x00 && data[3] == 0x2A))) return true;
        // ICO/CUR
        return data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00 && (data[2] == 0x01 || data[2] == 0x02) && data[3] == 0x00;
    }

    private bool TryGet(string key, out BitmapSource? image)
    {
        lock (_sync)
        {
            if (!_cache.TryGetValue(key, out var entry)) { image = null; return false; }
            _lru.Remove(entry.Node); _lru.AddFirst(entry.Node); image = entry.Image; return true;
        }
    }
    private BitmapSource? Remember(string key, BitmapSource? image)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(key, out var existing)) _lru.Remove(existing.Node);
            var node = _lru.AddFirst(key); _cache[key] = (image, node);
            while (_cache.Count > 128) { var last = _lru.Last!; _cache.Remove(last.Value); _lru.RemoveLast(); }
        }
        return image;
    }
    public void Dispose() { _shutdown.Cancel(); client.Dispose(); lock (_sync) { _cache.Clear(); _lru.Clear(); } }
}
