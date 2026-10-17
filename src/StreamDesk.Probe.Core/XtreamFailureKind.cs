namespace StreamDesk.Probe.Core;

public enum XtreamFailureKind
{
    InvalidEndpoint,
    InvalidCredentials,
    AccountExpired,
    ProviderUnavailable,
    RequestTimedOut,
    AccessDenied,
    UnsupportedResponse,
    FormatNotSupported,
    InvalidState
}
