using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public interface IPlaybackService : IDisposable, IAsyncDisposable
{
    object? PlayerHandle { get; }
    bool IsReady { get; }
    bool HasActiveMedia { get; }
    PlaybackSnapshot Snapshot { get; }
    PlaybackOptions Options { get; set; }
    int Volume { get; set; }
    bool IsMuted { get; set; }
    int AmplificationDb { get => 0; set { } }
    event EventHandler<PlaybackSnapshot>? StateChanged;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task PlayAsync(Uri source, CancellationToken cancellationToken = default);
    Task StopAsync();
    Task PauseAsync() => Task.CompletedTask;
    Task ResumeAsync() => Task.CompletedTask;
    Task<MediaPlaybackDetails> ReadDetailsAsync(CancellationToken cancellationToken = default) => Task.FromResult(MediaPlaybackDetails.Empty);
    Task<bool> SeekAsync(long milliseconds, CancellationToken cancellationToken = default) => Task.FromResult(false);
    Task<bool> SelectTrackAsync(bool subtitles, int id, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

public enum PlaybackSignalKind { Playing, Buffering, Paused, Failed, Ended }
public sealed record PlaybackSignal(long AttemptId, PlaybackSignalKind Kind, PlaybackFailure Failure = PlaybackFailure.None, float BufferPercent = 0);

public interface IPlaybackBackend : IAsyncDisposable
{
    object? PlayerHandle { get; }
    bool IsReady { get; }
    event EventHandler<PlaybackSignal>? Signal;
    Task InitializeAsync(CancellationToken cancellationToken);
    Task OpenAsync(Uri source, PlaybackOptions options, long attemptId, CancellationToken cancellationToken);
    Task StopAsync();
    Task PauseAsync() => Task.CompletedTask;
    Task ResumeAsync() => Task.CompletedTask;
    Task SetAudioAsync(int volume, bool muted);
    Task SetAmplificationAsync(int amplificationDb) => Task.CompletedTask;
    Task<MediaPlaybackDetails> ReadDetailsAsync(CancellationToken cancellationToken) => Task.FromResult(MediaPlaybackDetails.Empty);
    Task<bool> SeekAsync(long milliseconds, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> SelectTrackAsync(bool subtitles, int id, CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed record PlaybackPolicy(TimeSpan StartupTimeout, TimeSpan RetryDelay)
{
    public static PlaybackPolicy Default { get; } = new(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(1));
}
