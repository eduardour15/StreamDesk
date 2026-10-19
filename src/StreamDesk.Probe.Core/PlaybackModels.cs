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
        PlaybackFailure.Unauthorized or PlaybackFailure.Forbidden => "El proveedor rechazó el acceso. Revisa tu cuenta.",
        PlaybackFailure.Expired => "Tu cuenta venció. Renueva el acceso con tu proveedor.",
        PlaybackFailure.ConnectionLimit => "Alcanzaste el límite de pantallas simultáneas de tu cuenta.",
        PlaybackFailure.NotFound => "Este canal no está disponible ahora. Intenta actualizar la lista.",
        PlaybackFailure.InvalidSource => "La dirección del canal no es válida.",
        PlaybackFailure.Timeout => "El canal tardó demasiado en responder. Puedes reintentar.",
        PlaybackFailure.Network => "Se interrumpió la señal del canal.",
        PlaybackFailure.UnsupportedFormat => "El formato de video de este canal no es compatible.",
        PlaybackFailure.HardwareDecoder => "Ajustando modo de video compatible…",
        PlaybackFailure.Unavailable => "El motor de video no está disponible. Reinstala la aplicación.",
        PlaybackFailure.Unknown => "No se pudo abrir este canal. Prueba otro canal o formato.",
        PlaybackFailure.Cancelled => "Reproducción cancelada.",
        _ => State switch
        {
            PlaybackState.Loading => "Conectando con el canal…",
            PlaybackState.Playing => "Reproduciendo en vivo",
            PlaybackState.Buffering => BufferPercent > 0 ? $"Cargando señal ({BufferPercent:0}%)…" : "Cargando señal…",
            PlaybackState.Stopped => "Reproducción detenida.",
            _ => "Haz clic en un canal para empezar a ver."
        }
    };
}

public sealed class PlaybackException(PlaybackFailure failure) : Exception(new PlaybackSnapshot(0, PlaybackState.Failed, failure).Message)
{
    public PlaybackFailure Failure { get; } = failure;
}
