using StreamDesk.Probe.Core;
namespace StreamDesk.Application;

/// <summary>In-memory playlist catalog. Stream URLs deliberately stay out of CatalogSnapshot and persistence.</summary>
public sealed record ImportedPlaylist(CatalogSnapshot Catalog, IReadOnlyDictionary<string, Uri> StreamUris);

public interface IPlaylistImporter
{
    Task<ImportedPlaylist> ImportLocalAsync(string path, CancellationToken cancellationToken = default);
    Task<ImportedPlaylist> ImportRemoteAsync(Uri source, CancellationToken cancellationToken = default);
    Task EnsureSafeStreamUriAsync(Uri streamUri, CancellationToken cancellationToken = default);
}

public sealed class PlaylistImportException(string message) : Exception(message);

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
    Task UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default);
    Task<AppSettings> GetAppSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveAppSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
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
        var profile = new Profile(id, normalizedName, uri!.ToString().TrimEnd('/'), id, DateTimeOffset.UtcNow, "xtream");
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

    public async Task CreateM3uProfileAsync(
        string name,
        string sourcePathOrUrl,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 80)
            throw new ArgumentException("El nombre debe tener entre 1 y 80 caracteres.", nameof(name));
        var trimmedSource = sourcePathOrUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedSource))
            throw new ArgumentException("Ingresa un archivo o dirección M3U válida.", nameof(sourcePathOrUrl));

        var isHttp = trimmedSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     trimmedSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (isHttp)
        {
            if (!Uri.TryCreate(trimmedSource, UriKind.Absolute, out _))
                throw new ArgumentException("La dirección URL de la lista no es válida.", nameof(sourcePathOrUrl));
        }
        else
        {
            if (!File.Exists(trimmedSource))
                throw new ArgumentException("El archivo de lista M3U no existe en este equipo.", nameof(sourcePathOrUrl));
        }

        var id = Guid.NewGuid().ToString("N");
        var profile = new Profile(id, normalizedName, trimmedSource, id, DateTimeOffset.UtcNow, "m3u");
        await credentials.SaveAsync(id, new StoredCredentials("m3u", "m3u"), cancellationToken);
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

    public async Task UpdateAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        if (profile is null) throw new ArgumentNullException(nameof(profile));
        var normalizedName = profile.Name.Trim();
        if (normalizedName.Length is < 1 or > 80)
            throw new ArgumentException("El nombre debe tener entre 1 y 80 caracteres.", nameof(profile));
        await settings.UpdateProfileAsync(profile with { Name = normalizedName }, cancellationToken);
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
