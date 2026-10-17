namespace StreamDesk.Probe.Core;

public sealed record Profile(
    string Id,
    string Name,
    string BaseUrl,
    string CredentialKey,
    DateTimeOffset CreatedAt);

public sealed record StoredCredentials(string Username, string Password);
