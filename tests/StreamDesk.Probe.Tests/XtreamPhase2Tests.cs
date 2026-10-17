using System.Net;
using System.Net.Http.Headers;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class XtreamPhase2Tests
{
    private const string Account = """{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}""";
    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    private static XtreamApiClient Client(Handler handler, int retries = 0) => new("https://provider.invalid", "fixture-user", "fixture-password", handler: handler, maxRetries: retries);

    [Theory]
    [InlineData("https://provider.invalid/live/fixture-user/fixture-password/42.ts")]
    [InlineData("https://provider.invalid/movie/fixture-user/fixture-password/42")]
    [InlineData("https://provider.invalid/player_api.php")]
    [InlineData("https://provider.invalid/get.php?username=fixture-user&password=fixture-password")]
    public void RejectsAuthenticatedUrlsMistakenForBaseEndpoint(string url)
    {
        Assert.False(ProviderEndpoint.TryParse(url, out _));
        var error = Assert.Throws<XtreamApiException>(() => new XtreamApiClient(url, "fixture-user", "fixture-password"));
        Assert.Equal(XtreamFailureKind.InvalidEndpoint, error.Kind);
        Assert.DoesNotContain("fixture-password", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Banned")]
    [InlineData("Unknown")]
    public async Task RejectsNonActiveAccountsEvenWithAuthOne(string status)
    {
        using var client = Client(new Handler((_, _) => Task.FromResult(Json(System.Text.Json.JsonSerializer.Serialize(new { user_info = new { auth = 1, status } })))));
        var error = await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());
        Assert.Equal(XtreamFailureKind.AccessDenied, error.Kind);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(5, 0, false)]
    [InlineData(-1, -1, false)]
    public async Task ParsesConnectionLimitsWithoutInventingUnknownValues(int active, int max, bool reached)
    {
        using var client = Client(new Handler((_, _) => Task.FromResult(Json(System.Text.Json.JsonSerializer.Serialize(new { user_info = new { auth = 1, status = "Active", active_cons = active.ToString(), max_connections = max } })))));
        var account = await client.AuthenticateAsync();
        Assert.Equal(reached, account.HasReachedConnectionLimit);
        if (active < 0) Assert.Null(account.ActiveConnections);
        if (max < 0) Assert.Null(account.MaxConnections);
    }

    [Fact]
    public async Task FailedReauthenticationInvalidatesPreviousSession()
    {
        var calls = 0;
        using var client = Client(new Handler((_, _) => Task.FromResult(Json(++calls == 1 ? Account : """{"user_info":{"auth":0}}"""))));
        await client.AuthenticateAsync();
        await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());
        var error = await Assert.ThrowsAsync<XtreamApiException>(() => client.GetLiveChannelsAsync());
        Assert.Equal(XtreamFailureKind.InvalidState, error.Kind);
    }

    [Fact]
    public async Task NormalizesCatalogAndPreservesRemoteIdsAndRepeatedNames()
    {
        using var client = Client(new Handler((request, _) => Task.FromResult(Json(
            request.RequestUri!.Query.Contains("action=get_live_streams", StringComparison.Ordinal)
            ? """[{"stream_id":" 0042 ","name":" Águila ","stream_icon":"https://logos.invalid/a.png","epg_channel_id":"epg-a"},{"stream_id":"0042","name":"Duplicado"},{"stream_id":43,"name":"Águila"}]"""
            : Account))));
        await client.AuthenticateAsync();
        var channels = await client.GetLiveChannelsAsync();
        Assert.Equal(2, channels.Count);
        Assert.Equal("0042", channels[0].Id);
        Assert.Equal("Águila", channels[0].Name);
        Assert.Equal("https://logos.invalid/a.png", channels[0].LogoUrl);
        Assert.Equal("epg-a", channels[0].EpgChannelId);
        Assert.Null(channels[1].LogoUrl);
    }

    [Theory]
    [InlineData("https://logos.invalid/a.png?token=example")]
    [InlineData("https://fixture-user:fixture-password@logos.invalid/a.png")]
    [InlineData("https://logos.invalid/fixture-password/a.png")]
    [InlineData("file:///sample.png")]
    public async Task DiscardsPotentiallyCredentialBearingOrUnsupportedLogos(string logo)
    {
        using var client = Client(new Handler((request, _) => Task.FromResult(Json(request.RequestUri!.Query.Contains("action=", StringComparison.Ordinal)
            ? $$"""[{"stream_id":1,"name":"Canal","stream_icon":"{{logo}}"}]""" : Account))));
        await client.AuthenticateAsync();
        Assert.Null(Assert.Single(await client.GetLiveChannelsAsync()).LogoUrl);
    }

    [Fact]
    public async Task RejectsPartialMalformedCatalogInsteadOfReturningDestructiveSubset()
    {
        using var client = Client(new Handler((request, _) => Task.FromResult(Json(request.RequestUri!.Query.Contains("action=", StringComparison.Ordinal)
            ? """[{"stream_id":1,"name":"Canal"},{"name":"Sin ID"}]""" : Account))));
        await client.AuthenticateAsync();
        var error = await Assert.ThrowsAsync<XtreamApiException>(() => client.GetLiveChannelsAsync());
        Assert.Equal(XtreamFailureKind.UnsupportedResponse, error.Kind);
    }

    [Fact]
    public async Task RetriesTransientGetAndHonorsZeroRetryAfter()
    {
        var calls = 0;
        using var client = Client(new Handler((_, _) =>
        {
            var response = ++calls == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(Account);
            if (calls == 1) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }), retries: 2);
        await client.AuthenticateAsync();
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.BadRequest, 1)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 3)]
    public async Task RetryBudgetAppliesOnlyToTransientResponses(HttpStatusCode status, int expected)
    {
        var calls = 0;
        using var client = Client(new Handler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }), retries: 2);
        await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());
        Assert.Equal(expected, calls);
    }

    [Fact]
    public async Task LongRetryAfterIsNotRetriedEarly()
    {
        var calls = 0;
        using var client = Client(new Handler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return Task.FromResult(response);
        }), retries: 2);
        var error = await Assert.ThrowsAsync<XtreamApiException>(() => client.AuthenticateAsync());
        Assert.Equal(XtreamFailureKind.RateLimited, error.Kind);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CatalogQueriesAreSerializedAndWaitingQueryCanBeCancelled()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = 0;
        using var client = Client(new Handler(async (request, token) =>
        {
            if (!request.RequestUri!.Query.Contains("action=", StringComparison.Ordinal)) return Json(Account);
            Interlocked.Increment(ref queries); entered.SetResult();
            await release.Task.WaitAsync(token);
            return Json("[]");
        }));
        await client.AuthenticateAsync();
        var first = client.GetLiveChannelsAsync();
        await entered.Task;
        using var cancellation = new CancellationTokenSource();
        var second = client.GetLiveCategoriesAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Equal(1, queries);
        release.SetResult(); await first;
    }
}
