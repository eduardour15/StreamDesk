using System.Globalization;
using System.Net;
using System.Text.Json;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed partial class XtreamApiClient : IIptvProvider, IContentProvider
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "ts",
        "m3u8"
    };

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maxRetries;
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Uri? _baseUri;
    private string? _username;
    private string? _password;
    private XtreamAccount? _account;
    private bool _disposed;

    public XtreamApiClient(
        string baseUrl,
        string username,
        string password,
        TimeSpan? requestTimeout = null,
        HttpMessageHandler? handler = null,
        HttpClient? httpClient = null,
        int maxRetries = 2)
    {
        if (!ProviderEndpoint.TryParse(baseUrl, out var baseUri))
        {
            throw new XtreamApiException(XtreamFailureKind.InvalidEndpoint);
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            throw new XtreamApiException(XtreamFailureKind.InvalidCredentials);
        }

        _baseUri = baseUri;
        _username = username.Trim();
        _password = password;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15);

        if (_requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }

        if (maxRetries is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(maxRetries));
        _maxRetries = maxRetries;
        _httpClient = httpClient ?? new HttpClient(handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            MaxConnectionsPerServer = 2
        }, disposeHandler: true);
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public bool UsesUnencryptedTransport =>
        _baseUri?.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) == true;

    public async Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _account = null;
        var root = await GetJsonAsync(BuildApiUri(action: null), isAuthentication: true, cancellationToken)
            .ConfigureAwait(false);

        _account = ParseAccount(root);
        return _account;
    }

    public async Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var root = await GetJsonAsync(BuildApiUri("get_live_categories"), isAuthentication: false, cancellationToken)
            .ConfigureAwait(false);
        return ParseArray(root, ParseCategory, item => item.Id);
    }

    public async Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var root = await GetJsonAsync(BuildApiUri("get_live_streams"), isAuthentication: false, cancellationToken)
            .ConfigureAwait(false);
        return ParseArray(root, ParseChannel, item => item.Id);
    }

    public Uri CreateLiveStreamUri(XtreamChannel channel, string format)
    {
        ThrowIfDisposed();
        EnsureAuthenticated();

        var normalizedFormat = format.Trim().TrimStart('.').ToLowerInvariant();
        if (!SupportedFormats.Contains(normalizedFormat) ||
            !_account!.AllowedOutputFormats.Contains(normalizedFormat, StringComparer.OrdinalIgnoreCase))
        {
            throw new XtreamApiException(XtreamFailureKind.FormatNotSupported);
        }

        if (string.IsNullOrWhiteSpace(channel.Id))
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var baseUri = _baseUri!;
        var escapedUser = EscapePathSegment(_username!);
        var escapedPassword = EscapePathSegment(_password!);
        var escapedStreamId = Uri.EscapeDataString(channel.Id);
        var path = AppendPath(baseUri.AbsolutePath, $"live/{escapedUser}/{escapedPassword}/{escapedStreamId}.{normalizedFormat}");
        // All variable segments are escaped above and the base endpoint is validated.
        // Preserve encoded dot-only credentials instead of Uri collapsing their path.
        return new Uri(baseUri.GetLeftPart(UriPartial.Authority) + path,
            new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _password = null;
        _username = null;
        _account = null;
        _baseUri = null;
        _httpClient.Dispose();
        _lifetime.Dispose();
    }

    private async Task<JsonElement> GetJsonAsync(
        Uri requestUri,
        bool isAuthentication,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeoutSource.CancelAfter(_requestTimeout);
        var entered = false;
        try
        {
            await _requests.WaitAsync(timeoutSource.Token).ConfigureAwait(false);
            entered = true;
            using var response = await SendWithRetriesAsync(requestUri, timeoutSource.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var kind = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden when isAuthentication =>
                        XtreamFailureKind.InvalidCredentials,
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => XtreamFailureKind.AccessDenied,
                    HttpStatusCode.TooManyRequests => XtreamFailureKind.RateLimited,
                    _ => XtreamFailureKind.ProviderUnavailable
                };
                throw new XtreamApiException(kind);
            }

            if (response.Content.Headers.ContentLength > 32 * 1024 * 1024)
                throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
            await response.Content.LoadIntoBufferAsync(32 * 1024 * 1024, timeoutSource.Token).ConfigureAwait(false);
            await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: timeoutSource.Token)
                .ConfigureAwait(false);
            return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _disposed)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new XtreamApiException(XtreamFailureKind.RequestTimedOut);
        }
        catch (HttpRequestException)
        {
            throw new XtreamApiException(XtreamFailureKind.ProviderUnavailable);
        }
        catch (JsonException)
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }
        finally
        {
            if (entered) _requests.Release();
        }
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
            if (!transient || attempt >= _maxRetries) return response;
            var retryAfter = response.Headers.RetryAfter;
            var delay = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow)
                ?? TimeSpan.FromMilliseconds(250 * (1 << attempt));
            // A long Retry-After is surfaced to the user; never retry earlier than requested.
            if (delay > TimeSpan.FromSeconds(5)) return response;
            response.Dispose();
            await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private Uri BuildApiUri(string? action, string? parameter = null, string? value = null)
    {
        ThrowIfDisposed();
        var baseUri = _baseUri!;
        var query = $"username={Uri.EscapeDataString(_username!)}&password={Uri.EscapeDataString(_password!)}";
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = $"action={Uri.EscapeDataString(action)}&{query}";
        }
        if (parameter is not null) query += $"&{Uri.EscapeDataString(parameter)}={Uri.EscapeDataString(value ?? "")}";

        return new UriBuilder(baseUri)
        {
            Path = AppendPath(baseUri.AbsolutePath, "player_api.php"),
            Query = query,
            Fragment = string.Empty
        }.Uri;
    }

    private static XtreamAccount ParseAccount(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("user_info", out var userInfo) ||
            userInfo.ValueKind != JsonValueKind.Object)
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var status = ReadString(userInfo, "status") ?? string.Empty;
        var auth = ReadBoolean(userInfo, "auth");
        var expiresAt = ReadUnixDate(userInfo, "exp_date");

        if (status.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
            (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow))
        {
            throw new XtreamApiException(XtreamFailureKind.AccountExpired);
        }

        if (auth == false)
        {
            throw new XtreamApiException(XtreamFailureKind.InvalidCredentials);
        }

        if (!string.IsNullOrEmpty(status) && !status.Equals("active", StringComparison.OrdinalIgnoreCase))
            throw new XtreamApiException(XtreamFailureKind.AccessDenied);

        if (auth is null && !status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var activeConnections = ReadInt(userInfo, "active_cons");
        var maxConnections = ReadInt(userInfo, "max_connections");
        if (activeConnections < 0) activeConnections = null;
        if (maxConnections < 0) maxConnections = null;
        var isTrial = ReadBoolean(userInfo, "is_trial");
        var formats = ReadStringArray(userInfo, "allowed_output_formats")
            .Select(NormalizeFormat)
            .Where(format => SupportedFormats.Contains(format))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new XtreamAccount(
            string.IsNullOrWhiteSpace(status) ? "Autenticada" : status,
            activeConnections,
            maxConnections,
            isTrial,
            expiresAt,
            formats);
    }

    private static IReadOnlyList<T> ParseArray<T>(JsonElement root, Func<JsonElement, T> parseItem, Func<T, string> key)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var results = new List<T>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.EnumerateArray())
        {
            var parsed = parseItem(item);
            if (seen.Add(key(parsed))) results.Add(parsed);
        }

        return results;
    }

    private static XtreamCategory ParseCategory(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var id = ReadString(item, "category_id") ?? ReadString(item, "id");
        var name = ReadString(item, "category_name") ?? ReadString(item, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        return new XtreamCategory(id.Trim(), name.Trim());
    }

    private XtreamChannel ParseChannel(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var id = ReadString(item, "stream_id");
        var name = ReadString(item, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        return new XtreamChannel(
            id.Trim(),
            name.Trim(),
            ReadString(item, "category_id")?.Trim(),
            ReadString(item, "stream_type")?.Trim(),
            ReadString(item, "container_extension")?.Trim(),
            SafeLogoUrl(ReadString(item, "stream_icon")),
            ReadString(item, "epg_channel_id"));
    }

    private string? SafeLogoUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            return null;
        var decoded = Uri.UnescapeDataString(uri.AbsoluteUri);
        if (decoded.Contains(_username!, StringComparison.OrdinalIgnoreCase) || decoded.Contains(_password!, StringComparison.OrdinalIgnoreCase))
            return null;
        return uri.AbsoluteUri;
    }

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static int? ReadInt(JsonElement parent, string propertyName)
    {
        var value = ReadString(parent, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static bool? ReadBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        var text = ReadString(parent, propertyName);
        if (bool.TryParse(text, out var boolean))
        {
            return boolean;
        }

        return text switch
        {
            "1" => true,
            "0" => false,
            _ => null
        };
    }

    private static DateTimeOffset? ReadUnixDate(JsonElement parent, string propertyName)
    {
        var value = ReadString(parent, propertyName);
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds) || unixSeconds <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static string AppendPath(string existingPath, string suffix) =>
        $"{existingPath.TrimEnd('/')}/{suffix.TrimStart('/')}";

    private static string NormalizeFormat(string value) => value.Trim().TrimStart('.').ToLowerInvariant();

    private static string EscapePathSegment(string value) => value is "." or ".."
        ? value.Replace(".", "%2E", StringComparison.Ordinal) : Uri.EscapeDataString(value);

    private void EnsureAuthenticated()
    {
        ThrowIfDisposed();
        if (_account is null)
        {
            throw new XtreamApiException(XtreamFailureKind.InvalidState);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
