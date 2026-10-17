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
        XtreamFailureKind.InvalidEndpoint => "Introduce una URL base HTTP o HTTPS válida, sin usuario, contraseña ni parámetros.",
        XtreamFailureKind.InvalidCredentials => "El servidor no aceptó las credenciales.",
        XtreamFailureKind.AccountExpired => "El servidor indica que la cuenta está vencida.",
        XtreamFailureKind.ProviderUnavailable => "No se pudo conectar con el proveedor.",
        XtreamFailureKind.RequestTimedOut => "El proveedor tardó demasiado en responder.",
        XtreamFailureKind.AccessDenied => "El proveedor rechazó la solicitud.",
        XtreamFailureKind.RateLimited => "El proveedor pidió esperar. Intenta actualizar más tarde.",
        XtreamFailureKind.UnsupportedResponse => "El proveedor devolvió una respuesta que el prototipo no reconoce.",
        XtreamFailureKind.FormatNotSupported => "El formato solicitado no aparece entre los formatos declarados por el proveedor.",
        XtreamFailureKind.InvalidState => "Conecta una cuenta válida antes de consultar el catálogo o reproducir.",
        _ => "Ocurrió un error al consultar el proveedor."
    };
}
