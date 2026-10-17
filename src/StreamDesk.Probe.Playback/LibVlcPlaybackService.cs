using LibVLCSharp.Shared;
using StreamDesk.Application;

namespace StreamDesk.Probe.Playback;

public sealed class LibVlcPlaybackService : IPlaybackService
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _disposed;

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackFailed;
    public object? PlayerHandle => _mediaPlayer;
    public bool IsReady => _mediaPlayer is not null;
    public bool HasActiveMedia => _currentMedia is not null;

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mediaPlayer is not null) return;
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.Playing += (_, _) => PlaybackStarted?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.EncounteredError += (_, _) => PlaybackFailed?.Invoke(this, EventArgs.Empty);
    }

    public void Play(Uri streamUri)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_libVlc is null || _mediaPlayer is null) throw new InvalidOperationException("El motor de video no está disponible.");
        Stop();
        var media = new Media(_libVlc, streamUri);
        if (!_mediaPlayer.Play(media))
        {
            media.Dispose();
            throw new InvalidOperationException("LibVLC rechazó el medio solicitado.");
        }
        _currentMedia = media;
    }

    public void Stop()
    {
        if (_mediaPlayer is not null && _currentMedia is not null) _mediaPlayer.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _libVlc?.Dispose();
        _libVlc = null;
    }
}
