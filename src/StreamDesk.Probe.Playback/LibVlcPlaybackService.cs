using System.IO;
using System.Runtime.InteropServices;
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
        var runtimeDirectory = Path.Combine(AppContext.BaseDirectory, "libvlc", GetRuntimeArchitectureDirectory());
        if (!File.Exists(Path.Combine(runtimeDirectory, "libvlc.dll")) ||
            !File.Exists(Path.Combine(runtimeDirectory, "libvlccore.dll")))
        {
            throw new InvalidOperationException("No se encontró el runtime nativo de LibVLC junto al ejecutable.");
        }
        LibVLCSharp.Shared.Core.Initialize(runtimeDirectory);
        var libVlc = new LibVLC("--quiet", "--no-video-title-show");
        try
        {
            var player = new MediaPlayer(libVlc);
            player.Playing += (_, _) => PlaybackStarted?.Invoke(this, EventArgs.Empty);
            player.EncounteredError += (_, _) => PlaybackFailed?.Invoke(this, EventArgs.Empty);
            _libVlc = libVlc; _mediaPlayer = player;
        }
        catch { libVlc.Dispose(); throw; }
    }

    public void Play(Uri streamUri)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_libVlc is null || _mediaPlayer is null) throw new InvalidOperationException("El motor de video no está disponible.");
        Stop();
        var media = new Media(_libVlc, streamUri);
        try
        {
            if (!_mediaPlayer.Play(media)) throw new InvalidOperationException("LibVLC rechazó el medio solicitado.");
            _currentMedia = media;
        }
        catch { media.Dispose(); throw; }
    }

    public void Stop()
    {
        try { if (_mediaPlayer is not null && _currentMedia is not null) _mediaPlayer.Stop(); }
        finally { _currentMedia?.Dispose(); _currentMedia = null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Stop(); }
        finally
        {
            try { _mediaPlayer?.Dispose(); }
            finally { _mediaPlayer = null; _libVlc?.Dispose(); _libVlc = null; }
        }
    }

    private static string GetRuntimeArchitectureDirectory() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            var architecture => throw new PlatformNotSupportedException($"Arquitectura no soportada: {architecture}.")
        };
}
