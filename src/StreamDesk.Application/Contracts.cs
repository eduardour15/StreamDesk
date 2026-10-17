using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public interface IIptvProvider : IDisposable
{
    bool UsesUnencryptedTransport { get; }
    Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default);
    Uri CreateLiveStreamUri(XtreamChannel channel, string format);
}

public interface IPlaybackService : IDisposable
{
    object? PlayerHandle { get; }
    bool IsReady { get; }
    bool HasActiveMedia { get; }
    event EventHandler? PlaybackStarted;
    event EventHandler? PlaybackFailed;
    void Initialize();
    void Play(Uri streamUri);
    void Stop();
}

public interface ISettingsService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task AddProfileAsync(Profile profile, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default);
}

public interface ICredentialStore
{
    Task SaveAsync(string key, StoredCredentials credentials, CancellationToken cancellationToken = default);
    Task<StoredCredentials?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public interface IAppLogger
{
    void LogError(string eventId, Exception exception);
}

public sealed class ProfileService(ISettingsService settings, ICredentialStore credentials)
{
    public async Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        await settings.GetProfilesAsync(cancellationToken);

    public async Task CreateAsync(
        string name,
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 80)
            throw new ArgumentException("El nombre debe tener entre 1 y 80 caracteres.", nameof(name));
        if (string.IsNullOrWhiteSpace(username) || username.Length > 256 || string.IsNullOrEmpty(password) || password.Length > 1024)
            throw new ArgumentException("Revisa el usuario y la contraseña.", nameof(username));
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Usa una URL base HTTP o HTTPS sin credenciales ni parámetros.", nameof(baseUrl));

        var id = Guid.NewGuid().ToString("N");
        var profile = new Profile(id, normalizedName, uri.ToString().TrimEnd('/'), id, DateTimeOffset.UtcNow);
        await credentials.SaveAsync(id, new StoredCredentials(username.Trim(), password), cancellationToken);
        try
        {
            await settings.AddProfileAsync(profile, cancellationToken);
        }
        catch
        {
            await credentials.DeleteAsync(id, CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var profile = (await settings.GetProfilesAsync(cancellationToken)).FirstOrDefault(item => item.Id == id);
        if (profile is null)
            return;

        await settings.DeleteProfileAsync(id, cancellationToken);
        try
        {
            await credentials.DeleteAsync(profile.CredentialKey, cancellationToken);
        }
        catch
        {
            // Restore metadata so a transient credential-store error remains visible and retryable.
            await settings.AddProfileAsync(profile, CancellationToken.None);
            throw;
        }
    }
}
