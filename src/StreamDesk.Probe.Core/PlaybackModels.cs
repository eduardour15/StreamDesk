namespace StreamDesk.Probe.Core;

public enum PlaybackState { Idle, Loading, Playing, Buffering, Failed, Stopped }
public enum PlaybackFailure { None, Cancelled, InvalidSource, Unauthorized, Forbidden, NotFound, Expired, ConnectionLimit, Network, Timeout, HardwareDecoder, UnsupportedFormat, Unavailable, Unknown }
public enum HardwareDecoding { Automatic, Software }

public sealed record PlaybackOptions(int BufferMilliseconds = 1000, HardwareDecoding Hardware = HardwareDecoding.Automatic, bool OnDemand = false, long StartPositionMilliseconds = 0)
{
    public PlaybackOptions Validate()
    {
        if (BufferMilliseconds is < 250 or > 10000 || !Enum.IsDefined(Hardware) || StartPositionMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(BufferMilliseconds));
        return this;
    }
}

public sealed record PlaybackSnapshot(long Sequence, PlaybackState State, PlaybackFailure Failure = PlaybackFailure.None,
    int RetryCount = 0, float BufferPercent = 0, bool UsingSoftware = false, bool WillRetry = false, bool EndOfMedia = false)
{
    public bool IsActive => State is PlaybackState.Loading or PlaybackState.Playing or PlaybackState.Buffering;
    public string Message => Failure switch
    {
        PlaybackFailure.Unauthorized or PlaybackFailure.Forbidden => "El proveedor negó la reproducción. Comprueba tu cuenta y sus permisos.",
        PlaybackFailure.Expired => "La cuenta venció. Renueva el acceso antes de reproducir.",
        PlaybackFailure.ConnectionLimit => "La cuenta alcanzó su límite de conexiones. Detén otras reproducciones autorizadas.",
        PlaybackFailure.NotFound => "El canal no existe o ya no está disponible. Actualiza el catálogo.",
        PlaybackFailure.InvalidSource => "La dirección del canal no es válida.",
        PlaybackFailure.Timeout => "El canal tardó demasiado en responder. Puedes reintentar.",
        PlaybackFailure.Network => "Se interrumpió la conexión con el canal.",
        PlaybackFailure.UnsupportedFormat => "El motor no pudo decodificar el formato de este canal.",
        PlaybackFailure.HardwareDecoder => "Falló el decodificador de video. Prueba el modo por software.",
        PlaybackFailure.Unavailable => "No se pudo iniciar el motor de video. Reinstala la aplicación completa e inténtalo nuevamente.",
        PlaybackFailure.Unknown => "No se pudo abrir el canal. Comprueba autorización, formato y disponibilidad.",
        PlaybackFailure.Cancelled => "Reproducción cancelada.",
        _ => State switch
        {
            PlaybackState.Loading => "Abriendo canal…",
            PlaybackState.Playing => "Reproduciendo.",
            PlaybackState.Buffering => "Almacenando video en búfer…",
            PlaybackState.Stopped => "Reproducción detenida y recursos del medio liberados.",
            _ => "Selecciona un canal para reproducir."
        }
    };
}

public sealed class PlaybackException(PlaybackFailure failure) : Exception(new PlaybackSnapshot(0, PlaybackState.Failed, failure).Message)
{
    public PlaybackFailure Failure { get; } = failure;
}
