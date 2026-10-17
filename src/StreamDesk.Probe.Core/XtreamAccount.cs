namespace StreamDesk.Probe.Core;

public sealed record XtreamAccount(
    string Status,
    int? ActiveConnections,
    int? MaxConnections,
    bool? IsTrial,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> AllowedOutputFormats)
{
    public bool HasReachedConnectionLimit =>
        MaxConnections is > 0 && ActiveConnections >= MaxConnections;
}
