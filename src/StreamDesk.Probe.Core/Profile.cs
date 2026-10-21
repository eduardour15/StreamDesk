namespace StreamDesk.Probe.Core;

public sealed record Profile(
    string Id,
    string Name,
    string BaseUrl,
    string CredentialKey,
    DateTimeOffset CreatedAt,
    string ProviderType = "xtream");

public sealed record StoredCredentials(string Username, string Password);

public sealed record AppSettings(
    string Theme = "Dark",
    string UiScale = "100%",
    string Language = "es",
    bool ParentalControlEnabled = false,
    string? ParentalPinHash = null,
    string? ParentalPinSalt = null,
    int BufferMilliseconds = 3500,
    bool SoftwareDecoding = false);
