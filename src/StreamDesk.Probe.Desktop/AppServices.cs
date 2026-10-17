using System.IO;
using StreamDesk.Application;
using StreamDesk.Probe.Infrastructure;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Desktop;

public sealed class AppServices
{
    public AppServices(string localDataDirectory)
    {
        Directory.CreateDirectory(localDataDirectory);
        Logger = new SafeFileLogger(Path.Combine(localDataDirectory, "logs", "streamdesk.log"));
        Settings = new SqliteSettingsService(Path.Combine(localDataDirectory, "streamdesk.db"));
        Credentials = new DpapiCredentialStore(Path.Combine(localDataDirectory, "secrets"));
        Profiles = new ProfileService(Settings, Credentials);
        ProviderFactory = (url, username, password) => new XtreamApiClient(url, username, password);
        Playback = new LibVlcPlaybackService();
    }

    public IAppLogger Logger { get; }
    public ISettingsService Settings { get; }
    public ICredentialStore Credentials { get; }
    public ProfileService Profiles { get; }
    public Func<string, string, string, IIptvProvider> ProviderFactory { get; }
    public IPlaybackService Playback { get; }
}
