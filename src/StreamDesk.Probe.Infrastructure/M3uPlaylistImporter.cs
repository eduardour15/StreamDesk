using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

/// <summary>Imports bounded M3U data. Stream URIs remain transient and are validated again before playback.</summary>
public sealed class M3uPlaylistImporter : IPlaylistImporter, IDisposable
{
    public const int MaximumPlaylistBytes = 16 * 1024 * 1024;
    public const int MaximumChannels = 50_000;
    private const int MaximumLineLength = 16 * 1024;
    private const int MaximumLines = 200_000;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(20);
    private readonly HttpClient _client;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost;
    private readonly bool _ownsClient;
    private bool _disposed;

    public M3uPlaylistImporter()
        : this(CreateSafeClient(), ResolveHostAsync, ownsClient: true) { }

    /// <summary>Injected transport/resolver constructor for deterministic tests.</summary>
    public M3uPlaylistImporter(
        HttpClient client,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
        : this(client, resolveHost ?? ResolveHostAsync, ownsClient: false) { }

    private M3uPlaylistImporter(
        HttpClient client,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHost,
        bool ownsClient)
    {
        _client = client;
        _resolveHost = resolveHost;
        _ownsClient = ownsClient;
    }

    public async Task<ImportedPlaylist> ImportLocalAsync(string path, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            if (!Path.GetExtension(path).Equals(".m3u", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetExtension(path).Equals(".m3u8", StringComparison.OrdinalIgnoreCase))
                throw new PlaylistImportException("Selecciona un archivo .m3u o .m3u8.");

            var info = new FileInfo(path);
            if (!info.Exists) throw new PlaylistImportException("No se encontró el archivo seleccionado.");
            if (info.Length > MaximumPlaylistBytes) throw new PlaylistImportException("La lista supera el tamaño máximo de 16 MiB.");

            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var bytes = await ReadBoundedAsync(input, info.Length, cancellationToken);
            return await Task.Run(() => Parse(bytes, baseUri: null, cancellationToken), cancellationToken);
        }
        catch (PlaylistImportException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            throw new PlaylistImportException("No se pudo leer la lista seleccionada.");
        }
    }

    public async Task<ImportedPlaylist> ImportRemoteAsync(Uri source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSafeWebUri(source)) throw new PlaylistImportException("Usa una dirección HTTP o HTTPS válida, sin usuario ni contraseña en la dirección.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        try
        {
            await EnsurePublicHostAsync(source, timeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            request.Headers.Accept.ParseAdd("application/vnd.apple.mpegurl");
            request.Headers.Accept.ParseAdd("audio/x-mpegurl");
            request.Headers.Accept.ParseAdd("text/plain");
            request.Headers.AcceptEncoding.ParseAdd("identity");
            request.Headers.UserAgent.ParseAdd("StreamDesk/0.5");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
                throw new PlaylistImportException("El servidor redirigió la lista. Usa directamente la dirección final y prefiere HTTPS si está disponible.");
            if (!response.IsSuccessStatusCode)
                throw new PlaylistImportException("El servidor no permitió descargar la lista.");
            if (response.Content.Headers.ContentLength > MaximumPlaylistBytes)
                throw new PlaylistImportException("La lista supera el tamaño máximo de 16 MiB.");

            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            var bytes = await ReadBoundedAsync(input, response.Content.Headers.ContentLength, timeout.Token);
            return await Task.Run(() => Parse(bytes, source, timeout.Token), timeout.Token);
        }
        catch (PlaylistImportException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            throw new PlaylistImportException("La descarga superó el tiempo máximo de 20 segundos.");
        }
        catch (Exception)
        {
            throw new PlaylistImportException("No se pudo descargar la lista. Comprueba la dirección y la conexión.");
        }
    }

    public async Task EnsureSafeStreamUriAsync(Uri streamUri, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSafeWebUri(streamUri)) throw new PlaylistImportException("Este canal usa una dirección HTTP(S) no permitida.");
        await EnsurePublicHostAsync(streamUri, cancellationToken);
    }

    private async Task EnsurePublicHostAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var host = uri.DnsSafeHost;
            IPAddress[] addresses;
            if (IPAddress.TryParse(host, out var literal)) addresses = [literal];
            else addresses = await _resolveHost(host, cancellationToken);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
                throw new PlaylistImportException("La dirección apunta a una red privada o no pública y se rechazó por seguridad.");
        }
        catch (PlaylistImportException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            throw new PlaylistImportException("No se pudo validar la dirección de red.");
        }
    }

    private static bool IsSafeWebUri(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            return false;

        var host = uri.DnsSafeHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("home.arpa", StringComparison.OrdinalIgnoreCase))
            return false;

        return !IPAddress.TryParse(host, out var address) || IsPublicAddress(address);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6 || address.ScopeId != 0 || address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal)
                return false;

            var bytes = address.GetAddressBytes();
            // Permit only global-unicast 2000::/3, then exclude transition, special-use and documentation blocks.
            if ((bytes[0] & 0xE0) != 0x20) return false;
            var first32 = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
            if (first32 is >= 0x20010000 and <= 0x200101FF || // IETF special-purpose 2001::/23
                first32 == 0x20010DB8 || // documentation 2001:db8::/32
                (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && (bytes[3] & 0xF0) == 0x20) || // ORCHID
                (bytes[0] == 0x20 && bytes[1] == 0x02) || // 6to4
                (bytes[0] == 0x3F && bytes[1] == 0xFF && (bytes[2] & 0xF0) == 0x00)) // documentation 3fff::/20
                return false;
            return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = address.GetAddressBytes();
        var value = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        static bool In(uint ip, uint network, int prefix) => (ip & (uint.MaxValue << (32 - prefix))) == network;
        return !(In(value, 0x00000000, 8) || In(value, 0x0A000000, 8) || In(value, 0x64400000, 10) ||
                 In(value, 0x7F000000, 8) || In(value, 0xA9FE0000, 16) || In(value, 0xAC100000, 12) ||
                 In(value, 0xC0000000, 24) || In(value, 0xC0000200, 24) || In(value, 0xC0586300, 24) ||
                 In(value, 0xC0A80000, 16) || In(value, 0xC6120000, 15) || In(value, 0xC6336400, 24) ||
                 In(value, 0xCB007100, 24) || value >= 0xE0000000);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream input, long? contentLength, CancellationToken cancellationToken)
    {
        if (contentLength > MaximumPlaylistBytes)
            throw new PlaylistImportException("La lista supera el tamaño máximo de 16 MiB.");
        using var output = new MemoryStream(contentLength is > 0 and <= MaximumPlaylistBytes ? (int)contentLength.Value : 81920);
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + read > MaximumPlaylistBytes)
                throw new PlaylistImportException("La lista supera el tamaño máximo de 16 MiB.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static ImportedPlaylist Parse(byte[] bytes, Uri? baseUri, CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(input, new UTF8Encoding(false, false), detectEncodingFromByteOrderMarks: true);
        var categories = new List<XtreamCategory>();
        var categoryIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var channels = new List<XtreamChannel>();
        var streams = new Dictionary<string, Uri>(StringComparer.Ordinal);
        ChannelInfo? pending = null;
        var headerFound = false;
        var lineCount = 0;

        while (reader.ReadLine() is { } rawLine)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++lineCount > MaximumLines) throw new PlaylistImportException("La lista contiene demasiadas líneas.");
            if (rawLine.Length > MaximumLineLength) throw new PlaylistImportException("La lista contiene una línea demasiado larga.");
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (!headerFound)
            {
                if (line.Equals("#EXTM3U", StringComparison.OrdinalIgnoreCase) || line.StartsWith("#EXTM3U ", StringComparison.OrdinalIgnoreCase)) { headerFound = true; continue; }
                if (line.StartsWith('#')) continue;
                throw new PlaylistImportException("El archivo no tiene una cabecera M3U válida.");
            }

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                pending = ParseChannelInfo(line);
                continue;
            }
            if (line.StartsWith('#')) continue;
            if (pending is null) continue;

            if (channels.Count >= MaximumChannels)
                throw new PlaylistImportException("La lista supera el máximo de 50.000 canales.");

            if (TryResolveStreamUri(line, baseUri, out var streamUri))
            {
                var id = "m3u-" + StableHash(streamUri!.AbsoluteUri + "\0" + pending.Name);
                if (!streams.ContainsKey(id))
                {
                    string? categoryId = null;
                    if (!string.IsNullOrWhiteSpace(pending.Category))
                    {
                        if (!categoryIds.TryGetValue(pending.Category, out categoryId))
                        {
                            categoryId = "m3u-category-" + StableHash(pending.Category.Trim().ToUpperInvariant());
                            categoryIds.Add(pending.Category, categoryId);
                            categories.Add(new XtreamCategory(categoryId, pending.Category));
                        }
                    }

                    string? extension = Path.GetExtension(streamUri.AbsolutePath).TrimStart('.').ToLowerInvariant();
                    if (extension.Length is 0 or > 12) extension = null;
                    channels.Add(new XtreamChannel(id, pending.Name, categoryId, "live", extension,
                        LogoUrl: null, EpgChannelId: null));
                    streams.Add(id, streamUri);
                }
            }
            pending = null;
        }

        if (!headerFound) throw new PlaylistImportException("El archivo no tiene una cabecera M3U válida.");
        if (channels.Count == 0) throw new PlaylistImportException("La lista no contiene canales HTTP(S) válidos.");
        return new ImportedPlaylist(
            new CatalogSnapshot(categories, channels, DateTimeOffset.UtcNow, new HashSet<string>()), streams);
    }

    private static ChannelInfo? ParseChannelInfo(string line)
    {
        var start = line.IndexOf(':') + 1;
        var comma = FindNameSeparator(line, start);
        if (comma < 0) return null;
        var name = line[(comma + 1)..].Trim();
        if (name.Length is 0 or > 300) return null;
        var attributes = ParseAttributes(line[start..comma]);
        var category = attributes.GetValueOrDefault("group-title")?.Trim();
        if (category?.Length > 120) category = null;
        return new ChannelInfo(name, string.IsNullOrWhiteSpace(category) ? null : category);
    }

    private static int FindNameSeparator(string line, int start)
    {
        var quote = '\0';
        for (var index = start; index < line.Length; index++)
        {
            var character = line[index];
            if (quote != '\0') { if (character == quote) quote = '\0'; continue; }
            if (character is '\'' or '"') { quote = character; continue; }
            if (character == ',') return index;
        }
        return -1;
    }

    private static Dictionary<string, string> ParseAttributes(string text)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && !(char.IsAsciiLetterOrDigit(text[index]) || text[index] is '_' or '-')) index++;
            var keyStart = index;
            while (index < text.Length && (char.IsAsciiLetterOrDigit(text[index]) || text[index] is '_' or '-')) index++;
            if (keyStart == index) continue;
            var key = text[keyStart..index];
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            if (index >= text.Length || text[index] != '=') continue;
            index++;
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            if (index >= text.Length) break;
            var quote = text[index] is '\'' or '"' ? text[index++] : '\0';
            var valueStart = index;
            if (quote != '\0')
            {
                while (index < text.Length && text[index] != quote) index++;
                attributes[key] = text[valueStart..index];
                if (index < text.Length) index++;
            }
            else
            {
                while (index < text.Length && !char.IsWhiteSpace(text[index])) index++;
                attributes[key] = text[valueStart..index];
            }
        }
        return attributes;
    }

    private static bool TryResolveStreamUri(string value, Uri? baseUri, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var parsed)) return false;
        if (!parsed.IsAbsoluteUri)
        {
            if (baseUri is null || !Uri.TryCreate(baseUri, parsed, out parsed)) return false;
        }
        if (!IsSafeWebUri(parsed)) return false;
        uri = parsed;
        return true;
    }

    private static string StableHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24].ToLowerInvariant();

    private static HttpClient CreateSafeClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            MaxConnectionsPerServer = 2,
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            ConnectCallback = ConnectPublicAddressAsync
        };
        return new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static async ValueTask<Stream> ConnectPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await ResolveHostAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("La conexión a una dirección no pública fue rechazada.");

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                if (exception is OperationCanceledException) throw;
                lastError = exception;
            }
        }
        throw new HttpRequestException("No se pudo establecer la conexión de red.", lastError);
    }

    private static Task<IPAddress[]> ResolveHostAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsClient) _client.Dispose();
    }

    private sealed record ChannelInfo(string Name, string? Category);
}
