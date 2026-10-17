using System.Net;
using System.Text;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class XtreamApiClientTests
{
    [Fact]
    public async Task AuthenticateAsync_ParsesAccountLimitsAndDeclaredFormats()
    {
        using var client = CreateClient(_ => JsonResponse(
            """
            {"user_info":{"auth":1,"status":"Active","active_cons":"1","max_connections":"2","is_trial":"0","exp_date":"1893456000","allowed_output_formats":["ts","m3u8","mp4"]}}
            """));

        var account = await client.AuthenticateAsync();

        Assert.Equal("Active", account.Status);
        Assert.Equal(1, account.ActiveConnections);
        Assert.Equal(2, account.MaxConnections);
        Assert.False(account.IsTrial);
        Assert.Equal(new[] { "ts", "m3u8" }, account.AllowedOutputFormats);
    }

    [Fact]
    public async Task AuthenticateAsync_ClassifiesExpiredAccount()
    {
        using var client = CreateClient(_ => JsonResponse(
            """{"user_info":{"auth":0,"status":"Expired","allowed_output_formats":[]}}"""));

        var exception = await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());

        Assert.Equal(XtreamFailureKind.AccountExpired, exception.Kind);
    }

    [Fact]
    public async Task AuthenticateAsync_ClassifiesRejectedCredentialsWithoutEchoingThem()
    {
        using var client = CreateClient(_ => JsonResponse(
            """{"user_info":{"auth":0,"status":"Active"}}"""));

        var exception = await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());

        Assert.Equal(XtreamFailureKind.InvalidCredentials, exception.Kind);
        Assert.DoesNotContain("demo-user", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fake-password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogMethodsParseCategoriesAndChannelsAfterAuthentication()
    {
        using var client = CreateClient(request =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("action=get_live_categories", StringComparison.Ordinal))
            {
                return JsonResponse("""[{"category_id":"7","category_name":"Noticias"}]""");
            }

            if (query.Contains("action=get_live_streams", StringComparison.Ordinal))
            {
                return JsonResponse("""[{"stream_id":42,"name":"Canal de prueba","category_id":"7","stream_type":"live","container_extension":"ts"}]""");
            }

            return JsonResponse("""{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}""");
        });

        await client.AuthenticateAsync();
        var categories = await client.GetLiveCategoriesAsync();
        var channels = await client.GetLiveChannelsAsync();

        Assert.Collection(categories, category =>
        {
            Assert.Equal("7", category.Id);
            Assert.Equal("Noticias", category.Name);
        });
        Assert.Collection(channels, channel =>
        {
            Assert.Equal("42", channel.Id);
            Assert.Equal("Canal de prueba", channel.Name);
            Assert.Equal("7", channel.CategoryId);
            Assert.Equal("live", channel.StreamType);
        });

        var streamUri = client.CreateLiveStreamUri(Assert.Single(channels), "ts");
        Assert.Equal("provider.invalid", streamUri.Host);
        Assert.Equal("/base/live/demo-user/fake-password/42.ts", streamUri.AbsolutePath);
    }

    [Fact]
    public async Task CreateLiveStreamUri_UsesOnlyAFormatDeclaredByTheProvider()
    {
        using var client = CreateClient(_ => JsonResponse(
            """{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}"""));
        await client.AuthenticateAsync();

        var exception = Assert.Throws<XtreamApiException>(() =>
            client.CreateLiveStreamUri(new XtreamChannel("42", "Canal", null, "live", null), "m3u8"));

        Assert.Equal(XtreamFailureKind.FormatNotSupported, exception.Kind);
    }

    [Fact]
    public async Task CreateLiveStreamUri_EscapesEachCredentialAsAPathSegment()
    {
        using var client = new XtreamApiClient(
            "https://provider.invalid/base",
            "demo/name",
            "fake/pass",
            TimeSpan.FromSeconds(1),
            new StubHandler((_, _) => Task.FromResult(JsonResponse(
                """{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}"""))));
        await client.AuthenticateAsync();

        var uri = client.CreateLiveStreamUri(new XtreamChannel("42", "Canal", null, "live", null), "ts");

        Assert.Equal("/base/live/demo%2Fname/fake%2Fpass/42.ts", uri.AbsolutePath);
    }

    [Fact]
    public async Task AuthenticateAsync_MapsTimeoutToSafeError()
    {
        using var client = new XtreamApiClient(
            "https://provider.invalid",
            "demo-user",
            "fake-password",
            TimeSpan.FromMilliseconds(30),
            new StubHandler(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));

        var exception = await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());

        Assert.Equal(XtreamFailureKind.RequestTimedOut, exception.Kind);
        Assert.DoesNotContain("provider.invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_MapsNetworkErrorsWithoutEchoingRequestUri()
    {
        using var client = CreateClient((_, _) => throw new HttpRequestException("transport failure"));

        var exception = await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());

        Assert.Equal(XtreamFailureKind.ProviderUnavailable, exception.Kind);
        Assert.DoesNotContain("provider.invalid", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fake-password", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ftp://provider.invalid")]
    [InlineData("https://demo-user:fake-password@provider.invalid")]
    [InlineData("https://provider.invalid?token=fake")]
    public void Constructor_RejectsUnsafeOrUnsupportedBaseUrl(string baseUrl)
    {
        var exception = Assert.Throws<XtreamApiException>(() =>
            new XtreamApiClient(baseUrl, "demo-user", "fake-password"));

        Assert.Equal(XtreamFailureKind.InvalidEndpoint, exception.Kind);
        Assert.DoesNotContain("fake-password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsync_RespectsCallerCancellation()
    {
        using var client = CreateClient(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.AuthenticateAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("http://provider.invalid", true)]
    [InlineData("https://provider.invalid", false)]
    public void ProviderContractReportsUnencryptedTransportBeforeAuthentication(string endpoint, bool expected)
    {
        using IIptvProvider provider = new XtreamApiClient(endpoint, "demo-user", "fake-password");

        Assert.Equal(expected, provider.UsesUnencryptedTransport);
    }

    [Fact]
    public async Task ProviderContractRequiresAuthenticationBeforeCatalogOrPlayback()
    {
        var requests = 0;
        using var provider = CreateClient(_ =>
        {
            requests++;
            return JsonResponse("{}");
        });

        var categoriesError = await Assert.ThrowsAsync<XtreamApiException>(() => provider.GetLiveCategoriesAsync());
        var channelsError = await Assert.ThrowsAsync<XtreamApiException>(() => provider.GetLiveChannelsAsync());
        var playbackError = Assert.Throws<XtreamApiException>(() =>
            provider.CreateLiveStreamUri(new XtreamChannel("42", "Canal", null, "live", null), "ts"));

        Assert.Equal(XtreamFailureKind.InvalidState, categoriesError.Kind);
        Assert.Equal(XtreamFailureKind.InvalidState, channelsError.Kind);
        Assert.Equal(XtreamFailureKind.InvalidState, playbackError.Kind);
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task ProviderContractCancelsCatalogRequestWithoutMappingItToNetworkFailure()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = CreateClient(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.Query.Contains("action=get_live_streams", StringComparison.Ordinal))
            {
                requestStarted.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return JsonResponse("""{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}""");
        });
        await provider.AuthenticateAsync();
        using var cancellation = new CancellationTokenSource();

        var requestTask = provider.GetLiveChannelsAsync(cancellation.Token);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => requestTask);
    }

    [Fact]
    public async Task ProviderContractReturnsTypedCatalogErrorWithoutLeakingRequestData()
    {
        using var provider = CreateClient(request =>
            request.RequestUri!.Query.Contains("action=", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                : JsonResponse("""{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}"""));
        await provider.AuthenticateAsync();

        var error = await Assert.ThrowsAsync<XtreamApiException>(() => provider.GetLiveCategoriesAsync());

        Assert.Equal(XtreamFailureKind.AccessDenied, error.Kind);
        Assert.DoesNotContain("provider.invalid", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("demo-user", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("fake-password", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposingProviderContractReleasesTransportAndRejectsFurtherOperations()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse(
            """{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}""")));
        using IIptvProvider provider = new XtreamApiClient("https://provider.invalid", "demo-user", "fake-password", handler: handler);
        await provider.AuthenticateAsync();

        provider.Dispose();
        provider.Dispose();

        Assert.True(handler.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.AuthenticateAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.GetLiveCategoriesAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.GetLiveChannelsAsync());
        Assert.Throws<ObjectDisposedException>(() =>
            provider.CreateLiveStreamUri(new XtreamChannel("42", "Canal", null, "live", null), "ts"));
    }

    private static IIptvProvider CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        CreateClient((request, _) => Task.FromResult(responseFactory(request)));

    private static IIptvProvider CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        new XtreamApiClient(
            "https://provider.invalid/base",
            "demo-user",
            "fake-password",
            TimeSpan.FromSeconds(1),
            new StubHandler(responseFactory));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        public bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
