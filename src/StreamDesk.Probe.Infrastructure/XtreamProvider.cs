using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Infrastructure;

public sealed class XtreamProvider : IIptvProvider
{
    private readonly XtreamApiClient _client;

    public XtreamProvider(string baseUrl, string username, string password) =>
        _client = new XtreamApiClient(baseUrl, username, password);

    public bool UsesUnencryptedTransport => _client.UsesUnencryptedTransport;
    public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => _client.AuthenticateAsync(cancellationToken);
    public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => _client.GetLiveCategoriesAsync(cancellationToken);
    public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => _client.GetLiveChannelsAsync(cancellationToken);
    public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => _client.CreateLiveStreamUri(channel, format);
    public void Dispose() => _client.Dispose();
}
