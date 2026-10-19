using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using MediaTrack = StreamDesk.Probe.Core.MediaTrack;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Playback;

public sealed class LibVlcBackend : IPlaybackBackend
{
    private static readonly object CoreSync = new();
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private long _attempt;
    private int _failure;
    private bool _disposed;
    public object? PlayerHandle => _player;
    public bool IsReady => _player is not null && !_disposed;
    public event EventHandler<PlaybackSignal>? Signal;

    public Task InitializeAsync(CancellationToken token) => Task.Run(() =>
    {
        ObjectDisposedException.ThrowIf(_disposed, this); token.ThrowIfCancellationRequested();
        if (_player is not null) return;
        var architecture = RuntimeInformation.ProcessArchitecture switch
        { Architecture.X64 => "win-x64", Architecture.X86 => "win-x86", Architecture.Arm64 => "win-arm64", _ => throw new PlaybackException(PlaybackFailure.Unavailable) };
        var directory = Path.Combine(AppContext.BaseDirectory, "libvlc", architecture);
        if (!File.Exists(Path.Combine(directory, "libvlc.dll")) || !File.Exists(Path.Combine(directory, "libvlccore.dll")))
            throw new PlaybackException(PlaybackFailure.Unavailable);
        lock (CoreSync) LibVLCSharp.Shared.Core.Initialize(directory);
        var lib = new LibVLC("--no-video-title-show");
        try
        {
            // Subscribing replaces VLC's default logger. Raw messages are classified
            // only in memory and must never reach the application logger or UI.
            lib.Log += NativeLog;
            var player = new MediaPlayer(lib) { Scale = 0, EnableKeyInput = false, EnableMouseInput = false };
            player.Playing += Playing; player.Buffering += Buffering;
            player.Paused += Paused;
            player.EncounteredError += Failed; player.EndReached += Ended;
            _libVlc = lib; _player = player;
        }
        catch { lib.Dispose(); throw; }
    }, token);

    public Task OpenAsync(Uri source, PlaybackOptions options, long attemptId, CancellationToken token) => Task.Run(() =>
    {
        ObjectDisposedException.ThrowIf(_disposed, this); token.ThrowIfCancellationRequested(); options.Validate();
        if (_libVlc is null || _player is null) throw new PlaybackException(PlaybackFailure.Unavailable);
        if (_media is not null) throw new InvalidOperationException("El medio anterior debe detenerse antes de abrir otro.");
        Volatile.Write(ref _failure, (int)PlaybackFailure.None); Interlocked.Exchange(ref _attempt, attemptId);
        var media = new Media(_libVlc, source);
        media.AddOption(":network-caching=" + options.BufferMilliseconds);
        media.AddOption(":live-caching=" + options.BufferMilliseconds);
        media.AddOption(":file-caching=" + options.BufferMilliseconds);
        media.AddOption(options.Hardware == HardwareDecoding.Software ? ":avcodec-hw=none" : ":avcodec-hw=any");
        if (options.OnDemand && options.StartPositionMilliseconds > 0)
            media.AddOption(":start-time=" + (options.StartPositionMilliseconds / 1000d).ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            _media = media;
            if (!_player.Play(media)) throw new PlaybackException(PlaybackFailure.Unknown);
        }
        catch { _media = null; media.Dispose(); throw new PlaybackException(PlaybackFailure.Unknown); }
    }, token);

    public Task StopAsync() => Task.Run(StopCore);
    public Task PauseAsync() => Task.Run(() => { if (_player is not null && !_disposed) _player.SetPause(true); });
    public Task ResumeAsync() => Task.Run(() => { if (_player is not null && !_disposed) _player.SetPause(false); });
    private void StopCore()
    {
        Interlocked.Exchange(ref _attempt, 0);
        try { if (_media is not null) _player?.Stop(); }
        finally { _media?.Dispose(); _media = null; }
    }
    public Task SetAudioAsync(int volume, bool muted) => Task.Run(() =>
    { if (_player is not null && !_disposed) { _player.Volume = Math.Clamp(volume, 0, 100); _player.Mute = muted; } });

    private void Playing(object? sender, EventArgs args)
    { Volatile.Write(ref _failure, (int)PlaybackFailure.None); Emit(PlaybackSignalKind.Playing); }
    private void Paused(object? sender, EventArgs args) => Emit(PlaybackSignalKind.Paused);
    private void Buffering(object? sender, MediaPlayerBufferingEventArgs args) => Emit(PlaybackSignalKind.Buffering, percent: args.Cache);
    private void Failed(object? sender, EventArgs args) => Emit(PlaybackSignalKind.Failed, (PlaybackFailure)Volatile.Read(ref _failure));
    private void Ended(object? sender, EventArgs args) => Emit(PlaybackSignalKind.Ended);
    public Task<MediaPlaybackDetails> ReadDetailsAsync(CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        return _player is null || _disposed ? MediaPlaybackDetails.Empty : new MediaPlaybackDetails(Math.Max(0, _player.Time), Math.Max(0, _player.Length), _player.IsSeekable,
            _player.AudioTrackDescription.Select(item => new MediaTrack(item.Id, MediaMetadata.Text(item.Name, 256))).ToArray(),
            _player.SpuDescription.Select(item => new MediaTrack(item.Id, MediaMetadata.Text(item.Name, 256))).ToArray(), _player.AudioTrack, _player.Spu);
    }, token);
    public Task<bool> SeekAsync(long milliseconds, CancellationToken token) => Task.Run(() =>
    { token.ThrowIfCancellationRequested(); if (_player is null || !_player.IsSeekable || _disposed) return false; _player.Time = milliseconds; return true; }, token);
    public Task<bool> SelectTrackAsync(bool subtitles, int id, CancellationToken token) => Task.Run(() =>
    { token.ThrowIfCancellationRequested(); return _player is not null && !_disposed && (subtitles ? _player.SetSpu(id) : _player.SetAudioTrack(id)); }, token);
    private void Emit(PlaybackSignalKind kind, PlaybackFailure failure = PlaybackFailure.None, float percent = 0)
    { var attempt = Interlocked.Read(ref _attempt); if (attempt != 0 && !_disposed) Signal?.Invoke(this, new(attempt, kind, failure, percent)); }
    private void NativeLog(object? sender, LogEventArgs args)
    {
        if (Interlocked.Read(ref _attempt) == 0) return;
        var failure = NativeFailureClassifier.Classify(args.Module, args.Message);
        var previous = (PlaybackFailure)Volatile.Read(ref _failure);
        if (previous is PlaybackFailure.Unauthorized or PlaybackFailure.Forbidden or PlaybackFailure.NotFound) return;
        if (failure != PlaybackFailure.None) Volatile.Write(ref _failure, (int)failure);
    }

    public ValueTask DisposeAsync() => new(Task.Run(() =>
    {
        if (_disposed) return; _disposed = true;
        try { StopCore(); }
        finally
        {
            try
            {
                if (_player is not null)
                {
                    _player.Playing -= Playing; _player.Buffering -= Buffering;
                    _player.EncounteredError -= Failed; _player.EndReached -= Ended;
                    _player.Dispose();
                }
            }
            finally { _player = null; if (_libVlc is not null) { _libVlc.Log -= NativeLog; _libVlc.Dispose(); _libVlc = null; } }
        }
    }));
}
