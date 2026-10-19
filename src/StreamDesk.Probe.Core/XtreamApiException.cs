namespace StreamDesk.Probe.Core;

public sealed class XtreamApiException : Exception
{
    public XtreamApiException(XtreamFailureKind kind)
        : base(GetSafeMessage(kind))
    {
        Kind = kind;
    }

    public XtreamFailureKind Kind { get; }

    private static string GetSafeMessage(XtreamFailureKind kind) => kind switch
    {
        XtreamFailureKind.InvalidEndpoint => "Escribe solo la dirección del servidor (ej. https://tuservidor.com:8080).",
        XtreamFailureKind.InvalidCredentials => "Usuario o contraseña incorrectos.",
        XtreamFailureKind.AccountExpired => "Tu cuenta está vencida.",
        XtreamFailureKind.ProviderUnavailable => "No se pudo conectar con el servidor.",
        XtreamFailureKind.RequestTimedOut => "El servidor tardó demasiado en responder.",
        XtreamFailureKind.AccessDenied => "El servidor rechazó el acceso.",
        XtreamFailureKind.RateLimited => "Demasiadas consultas seguidas. Espera unos segundos.",
        XtreamFailureKind.UnsupportedResponse => "El servidor devolvió datos con un formato no válido.",
        XtreamFailureKind.FormatNotSupported => "Formato de video no admitido por el servidor.",
        XtreamFailureKind.InvalidState => "Conecta tu cuenta antes de continuar.",
        _ => "No se pudo comunicar con el servidor."
    };
}
