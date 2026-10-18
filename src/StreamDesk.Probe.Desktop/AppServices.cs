using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using StreamDesk.Application;
using StreamDesk.Probe.Infrastructure;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Desktop;

public sealed class AppServices : IDisposable
{
    private readonly ServiceProvider _httpServices;
    public AppServices(string localDataDirectory)
    {
        Directory.CreateDirectory(localDataDirectory);
        Logger = new SafeFileLogger(Path.Combine(localDataDirectory, "logs", "streamdesk.log"));
        Settings = new SqliteSettingsService(Path.Combine(localDataDirectory, "streamdesk.db"));
        Credentials = new DpapiCredentialStore(Path.Combine(localDataDirectory, "secrets"));
        Profiles = new ProfileService(Settings, Credentials);
        Catalogs = new CatalogService(new SqliteCatalogStore(Path.Combine(localDataDirectory, "streamdesk.db")));
        Viewer = new SqliteViewerStore(Path.Combine(localDataDirectory, "streamdesk.db"));
        Content = new SqliteContentStore(Path.Combine(localDataDirectory, "streamdesk.db"));
        Recovery = new LocalRecoveryService(localDataDirectory);
        _httpServices = ProviderHttpClients.CreateServices();
        var factory = _httpServices.GetRequiredService<IHttpClientFactory>();
        ProviderFactory = (url, username, password) => new XtreamApiClient(url, username, password, httpClient: factory.CreateClient("Xtream"));
        Logos = new ChannelLogoLoader(factory.CreateClient("Logos"));
        Playback = new PlaybackService(new LibVlcBackend());
    }

    public IAppLogger Logger { get; }
    public ISettingsService Settings { get; }
    public ICredentialStore Credentials { get; }
    public ProfileService Profiles { get; }
    public CatalogService Catalogs { get; }
    public IViewerStore Viewer { get; }
    public IContentStore Content { get; }
    public IRecoveryService Recovery { get; }
    public ChannelLogoLoader Logos { get; }
    public Func<string, string, string, IIptvProvider> ProviderFactory { get; }
    public IPlaybackService Playback { get; }
    public void Dispose() { Logos.Dispose(); Playback.Dispose(); _httpServices.Dispose(); (Logger as IDisposable)?.Dispose(); }
}
