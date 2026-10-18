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
        if (!ProviderEndpoint.TryParse(baseUrl, out var uri))
            throw new ArgumentException("Usa una URL base HTTP o HTTPS sin credenciales ni parámetros.", nameof(baseUrl));

        var id = Guid.NewGuid().ToString("N");
        var profile = new Profile(id, normalizedName, uri!.ToString().TrimEnd('/'), id, DateTimeOffset.UtcNow);
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

        // Keep the database (including cascaded catalog/favorites/history) intact
        // until the credential store has accepted deletion. Retain a memory-only
        // backup so a database failure can restore the protected credential.
        var backup = await credentials.GetAsync(profile.CredentialKey, cancellationToken);
        await credentials.DeleteAsync(profile.CredentialKey, cancellationToken);
        try
        {
            await settings.DeleteProfileAsync(id, cancellationToken);
        }
        catch
        {
            if (backup is not null)
                await credentials.SaveAsync(profile.CredentialKey, backup, CancellationToken.None);
            throw;
        }
    }
}
