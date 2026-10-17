using System.Globalization;
using System.Net;
using System.Text.Json;

namespace StreamDesk.Probe.Core;

public sealed class XtreamApiClient : IDisposable
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "ts",
        "m3u8"
    };

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;
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
        HttpMessageHandler? handler = null)
    {
        if (!TryParseBaseUrl(baseUrl, out var baseUri))
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

        _httpClient = new HttpClient(handler ?? new SocketsHttpHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public bool UsesUnencryptedTransport =>
        _baseUri?.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) == true;

    public async Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
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
        return ParseArray(root, ParseCategory);
    }

    public async Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var root = await GetJsonAsync(BuildApiUri("get_live_streams"), isAuthentication: false, cancellationToken)
            .ConfigureAwait(false);
        return ParseArray(root, ParseChannel);
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
        var escapedUser = Uri.EscapeDataString(_username!);
        var escapedPassword = Uri.EscapeDataString(_password!);
        var escapedStreamId = Uri.EscapeDataString(channel.Id);
        var builder = new UriBuilder(baseUri)
        {
            Path = AppendPath(baseUri.AbsolutePath, $"live/{escapedUser}/{escapedPassword}/{escapedStreamId}.{normalizedFormat}"),
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _password = null;
        _username = null;
        _account = null;
        _baseUri = null;
        _httpClient.Dispose();
    }

    private async Task<JsonElement> GetJsonAsync(
        Uri requestUri,
        bool isAuthentication,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_requestTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var kind = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden when isAuthentication =>
                        XtreamFailureKind.InvalidCredentials,
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => XtreamFailureKind.AccessDenied,
                    _ => XtreamFailureKind.ProviderUnavailable
                };
                throw new XtreamApiException(kind);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: timeoutSource.Token)
                .ConfigureAwait(false);
            return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
    }

    private Uri BuildApiUri(string? action)
    {
        ThrowIfDisposed();
        var baseUri = _baseUri!;
        var query = $"username={Uri.EscapeDataString(_username!)}&password={Uri.EscapeDataString(_password!)}";
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = $"action={Uri.EscapeDataString(action)}&{query}";
        }

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
            throw new XtreamApiException(XtreamFailureKind.InvalidCredentials);
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

        if (auth is null && !status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var activeConnections = ReadInt(userInfo, "active_cons");
        var maxConnections = ReadInt(userInfo, "max_connections");
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

    private static IReadOnlyList<T> ParseArray<T>(JsonElement root, Func<JsonElement, T> parseItem)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new XtreamApiException(XtreamFailureKind.UnsupportedResponse);
        }

        var results = new List<T>();
        foreach (var item in root.EnumerateArray())
        {
            results.Add(parseItem(item));
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

        return new XtreamCategory(id, name);
    }

    private static XtreamChannel ParseChannel(JsonElement item)
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
            id,
            name,
            ReadString(item, "category_id"),
            ReadString(item, "stream_type"),
            ReadString(item, "container_extension"));
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

    private static bool TryParseBaseUrl(string value, out Uri? baseUri)
    {
        baseUri = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(parsed.Host) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        baseUri = parsed;
        return true;
    }

    private static string AppendPath(string existingPath, string suffix) =>
        $"{existingPath.TrimEnd('/')}/{suffix.TrimStart('/')}";

    private static string NormalizeFormat(string value) => value.Trim().TrimStart('.').ToLowerInvariant();

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
