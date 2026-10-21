using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed partial class PlaybackService : IPlaybackService
{
    private readonly IPlaybackBackend _backend;
    private readonly PlaybackPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly object _sync = new();
    private Session? _session;
    private long _version, _attempt, _sequence;
    private bool _disposed;
    private Task? _disposeTask;
    private Task _progressMonitors = Task.CompletedTask;
    private long _audioRevision;
    private int _applyingAudio;
    public const int MaxAmplificationDb = 12;
    public const int MaxVolume = 400;
    private int _volume = 75;
    private int _amplificationDb;
    private bool _muted;
    private PlaybackOptions _options = new();
    private PlaybackSnapshot _snapshot = new(0, PlaybackState.Idle);

    public PlaybackService(IPlaybackBackend backend, PlaybackPolicy? policy = null, TimeProvider? clock = null)
    {
        _backend = backend; _policy = policy ?? PlaybackPolicy.Default; _clock = clock ?? TimeProvider.System;
        if (_policy.StartupTimeout <= TimeSpan.Zero || _policy.RetryDelay < TimeSpan.Zero || _policy.ProgressInterval <= TimeSpan.Zero || _policy.StallThreshold <= TimeSpan.Zero || _policy.StallTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(policy));
        _backend.Signal += BackendSignal;
    }
    public object? PlayerHandle => _backend.PlayerHandle;
    public bool IsReady => !_disposed && _backend.IsReady;
    public bool HasActiveMedia => Snapshot.IsActive;
    public PlaybackSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public PlaybackOptions Options { get { lock (_sync) return _options; } set { value.Validate(); lock (_sync) { ObjectDisposedException.ThrowIf(_disposed, this); _options = value; } } }
    public int Volume
    {
        get => Volatile.Read(ref _volume);
        set
        {
            var clamped = Math.Clamp(value, 0, MaxVolume);
            if (Interlocked.Exchange(ref _volume, clamped) != clamped)
            {
                var boost = clamped > 100 ? VolumeToDb(clamped) : 0;
                Volatile.Write(ref _amplificationDb, boost);
                QueueAudio();
            }
        }
    }
    public bool IsMuted { get => Volatile.Read(ref _muted); set { if (IsMuted == value) return; Volatile.Write(ref _muted, value); QueueAudio(); } }
    public int AmplificationDb
    {
        get => Volatile.Read(ref _amplificationDb);
        set
        {
            var clamped = Math.Clamp(value, 0, MaxAmplificationDb);
            if (Interlocked.Exchange(ref _amplificationDb, clamped) != clamped)
            {
                if (clamped > 0)
                {
                    Volatile.Write(ref _volume, DbToVolume(clamped));
                }
                else if (Volatile.Read(ref _volume) > 100)
                {
                    Volatile.Write(ref _volume, 100);
                }
                QueueAudio();
            }
        }
    }

    public static int VolumeToDb(int volume) => volume switch
    {
        <= 100 => 0,
        >= MaxVolume => MaxAmplificationDb,
        _ => Math.Clamp((int)Math.Round(20.0 * Math.Log10(volume / 100.0)), 1, MaxAmplificationDb)
    };

    public static int DbToVolume(int db) => db switch
    {
        <= 0 => 100,
        >= MaxAmplificationDb => MaxVolume,
        _ => Math.Clamp((int)Math.Round(100.0 * Math.Pow(10.0, db / 20.0)), 100, MaxVolume)
    };
    public event EventHandler<PlaybackSnapshot>? StateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed, this); await InitializeCoreAsync(cancellationToken).ConfigureAwait(false); if (!Snapshot.IsActive) Publish(PlaybackState.Idle); }
        finally { _serial.Release(); }
    }

    public async Task PlayAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (!source.IsAbsoluteUri || source.Scheme is not ("http" or "https" or "file")) throw new PlaybackException(PlaybackFailure.InvalidSource);
        cancellationToken.ThrowIfCancellationRequested();
        Session session;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var previous = _session;
            session = new Session(++_version, source, _options, cancellationToken);
            _session = session;
            previous?.Dispose();
            session.Registration = session.Token.Register(() => _ = CancelSessionAsync(session));
        }
        await _serial.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Current(session)) return;
            await _backend.StopAsync().ConfigureAwait(false);
            session.Token.ThrowIfCancellationRequested();
            await InitializeCoreAsync(session.Token).ConfigureAwait(false);
            await OpenAsync(session).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { if (Current(session)) { await _backend.StopAsync().ConfigureAwait(false); Publish(PlaybackState.Stopped, PlaybackFailure.Cancelled); } }
        catch (Exception exception) { if (Current(session)) await FailAsync(session, SafeFailure(exception)).ConfigureAwait(false); }
        finally { _serial.Release(); }
    }

    public async Task StopAsync()
    {
        long version;
        lock (_sync) { if (_disposed) return; version = ++_version; _session?.Dispose(); _session = null; }
        await _serial.WaitAsync().ConfigureAwait(false);
        try { if (version != _version || _disposed) return; await _backend.StopAsync().ConfigureAwait(false); Publish(PlaybackState.Stopped); }
        finally { _serial.Release(); }
    }

    public async Task PauseAsync()
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || Snapshot.State is not (PlaybackState.Playing or PlaybackState.Buffering)) return;
            await _backend.PauseAsync().ConfigureAwait(false);
            if (_session is { } paused) paused.UserPaused = true;
            _session?.CancelTimeout(); _session?.ResetProgress();
            Publish(PlaybackState.Paused);
        }
        finally { _serial.Release(); }
    }

    public async Task ResumeAsync()
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || Snapshot.State != PlaybackState.Paused) return;
            await _backend.ResumeAsync().ConfigureAwait(false);
            if (_session is { } resumed) resumed.UserPaused = false;
            _session?.ResetProgress();
            Publish(PlaybackState.Playing);
        }
        finally { _serial.Release(); }
    }

    private async Task InitializeCoreAsync(CancellationToken token)
    {
        if (_backend.IsReady) return;
        try { await _backend.InitializeAsync(token).ConfigureAwait(false); await _backend.SetAudioAsync(Math.Clamp(Volume, 0, 100), IsMuted).ConfigureAwait(false); await _backend.SetAmplificationAsync(AmplificationDb).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { Publish(PlaybackState.Failed, PlaybackFailure.Unavailable); throw new PlaybackException(PlaybackFailure.Unavailable); }
    }

    private async Task OpenAsync(Session session)
    {
        session.Token.ThrowIfCancellationRequested();
        session.HasPlayed = false;
        session.UserPaused = false;
        session.ResetProgress();
        session.Attempt = ++_attempt;
        Publish(PlaybackState.Loading, session: session);
        await _backend.SetAudioAsync(Math.Clamp(Volume, 0, 100), IsMuted).ConfigureAwait(false);
        await _backend.SetAmplificationAsync(AmplificationDb).ConfigureAwait(false);
        var effectiveBuffer = !session.Options.OnDemand && session.Retries > 0
            ? Math.Clamp(session.Options.BufferMilliseconds + (session.Retries * _policy.RetryBufferStepMilliseconds), 250, 10000)
            : session.Options.BufferMilliseconds;
        await _backend.OpenAsync(session.Source, session.Options with
        {
            BufferMilliseconds = effectiveBuffer,
            Hardware = session.Software ? HardwareDecoding.Software : session.Options.Hardware,
            StartPositionMilliseconds = session.ResumePosition
        }, session.Attempt, session.Token).ConfigureAwait(false);
        ArmTimeout(session, _policy.StartupTimeout);
        if (!session.MonitorStarted) { session.MonitorStarted = true; _progressMonitors = Task.WhenAll(_progressMonitors, MonitorProgressAsync(session)); }
    }

    private void BackendSignal(object? sender, PlaybackSignal signal) => _ = ProcessSignalAsync(signal);
    private async Task ProcessSignalAsync(PlaybackSignal signal)
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        Session? session = null;
        try
        {
            session = _session;
            if (session is null || !Current(session) || session.Token.IsCancellationRequested || session.Attempt != signal.AttemptId) return;
            switch (signal.Kind)
            {
                case PlaybackSignalKind.Playing:
                    if (session.UserPaused) break;
                    if (!session.HasPlayed) session.ResetProgress();
                    else session.LastProgressAt = _clock.GetTimestamp();
                    session.HasPlayed = true; session.CancelTimeout(); Publish(PlaybackState.Playing, session: session); break;
                case PlaybackSignalKind.Paused:
                    session.ResetProgress(); session.CancelTimeout(); Publish(PlaybackState.Paused, session: session); break;
                case PlaybackSignalKind.Buffering:
                    if (Snapshot.State == PlaybackState.Paused || session.SilentStall) break;
                    if (signal.BufferPercent >= 100) { if (session.HasPlayed) { session.LastProgressAt = _clock.GetTimestamp(); session.CancelTimeout(); Publish(PlaybackState.Playing, session: session); } else Publish(PlaybackState.Loading, session: session); }
                    else { if (Snapshot.State == PlaybackState.Playing) ArmTimeout(session, _policy.StartupTimeout); Publish(PlaybackState.Buffering, session: session, percent: signal.BufferPercent); }
                    break;
                case PlaybackSignalKind.Ended:
                    if (session.Options.OnDemand)
                    {
                        session.Attempt = 0; session.CancelTimeout();
                        await _backend.StopAsync().ConfigureAwait(false);
                        if (Current(session) && !session.Token.IsCancellationRequested) Publish(PlaybackState.Stopped, session: session, ended: true);
                    }
                    else await FailAsync(session, PlaybackFailure.Network).ConfigureAwait(false);
                    break;
                case PlaybackSignalKind.Failed:
                    await FailAsync(session, signal.Failure == PlaybackFailure.None ? PlaybackFailure.Unknown : signal.Failure).ConfigureAwait(false); break;
            }
        }
        catch { if (session is not null && Current(session)) Publish(PlaybackState.Failed, PlaybackFailure.Unknown); }
        finally { _serial.Release(); }
    }

    private async Task FailAsync(Session session, PlaybackFailure failure)
    {
        session.Attempt = 0; session.CancelTimeout();
        if (session.Options.OnDemand && session.HasPlayed)
        {
            try { var details = await _backend.ReadDetailsAsync(session.Token).ConfigureAwait(false); if (details.LengthMilliseconds > 0) session.ResumePosition = details.TimeMilliseconds; }
            catch (OperationCanceledException) { return; }
            catch { /* Keep the last known position if the backend cannot report it. */ }
        }
        else if (!session.Options.OnDemand && session.HasPlayed && failure == PlaybackFailure.Unknown)
        {
            // An unclassified error occurring mid-stream after live playback already succeeded is a transient stream drop.
            failure = PlaybackFailure.Network;
        }
        await _backend.StopAsync().ConfigureAwait(false);
        if (!Current(session) || session.Token.IsCancellationRequested) return;
        var hardwareFallback = failure == PlaybackFailure.HardwareDecoder && !session.Software && session.Options.Hardware == HardwareDecoding.Automatic;
        var retry = failure is PlaybackFailure.Network or PlaybackFailure.Timeout && session.Retries < _policy.MaxRetries;
        if (hardwareFallback) session.Software = true;
        else if (retry) session.Retries++;
        Publish(PlaybackState.Failed, failure, session, willRetry: hardwareFallback || retry);
        if (hardwareFallback) await OpenAsync(session).ConfigureAwait(false);
        else if (retry) _ = RecoverAsync(session, _policy.RetryDelay * session.Retries);
    }

    private async Task RecoverAsync(Session session, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _clock, session.Token).ConfigureAwait(false);
            await _serial.WaitAsync(session.Token).ConfigureAwait(false);
            try
            {
                if (!Current(session)) return;
                await OpenAsync(session).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { if (Current(session)) await FailAsync(session, SafeFailure(exception)).ConfigureAwait(false); }
            finally { _serial.Release(); }
        }
        catch (OperationCanceledException) { }
    }

    private void ArmTimeout(Session session, TimeSpan? timeoutOverride = null)
    {
        var token = session.RestartTimeout();
        var duration = timeoutOverride ?? _policy.StartupTimeout;
        if (token.HasValue) _ = WatchTimeoutAsync(session.Attempt, duration, token.Value);
    }
    private async Task WatchTimeoutAsync(long attempt, TimeSpan duration, CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, _clock, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
                await ProcessSignalAsync(new(attempt, PlaybackSignalKind.Failed, PlaybackFailure.Timeout)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }

    private async Task CancelSessionAsync(Session session)
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try { if (!Current(session)) return; session.Attempt = 0; await _backend.StopAsync().ConfigureAwait(false); Publish(PlaybackState.Stopped, PlaybackFailure.Cancelled); }
        catch { if (Current(session)) Publish(PlaybackState.Failed, PlaybackFailure.Unknown); }
        finally { _serial.Release(); }
    }

    private void QueueAudio()
    {
        Interlocked.Increment(ref _audioRevision);
        StartAudioUpdate();
    }
    private void StartAudioUpdate()
    {
        if (Interlocked.CompareExchange(ref _applyingAudio, 1, 0) == 0) _ = ApplyAudioAsync();
    }
    private async Task ApplyAudioAsync()
    {
        long revision = 0;
        try
        {
            do
            {
                await _serial.WaitAsync().ConfigureAwait(false);
                try
                {
                    revision = Volatile.Read(ref _audioRevision);
                    if (!_disposed && _backend.IsReady)
                    {
                        var vol = Volatile.Read(ref _volume);
                        var amp = Volatile.Read(ref _amplificationDb);
                        await _backend.SetAudioAsync(Math.Clamp(vol, 0, 100), IsMuted).ConfigureAwait(false);
                        await _backend.SetAmplificationAsync(amp).ConfigureAwait(false);
                    }
                }
                catch { /* Audio controls must not terminate an otherwise valid stream. */ }
                finally { _serial.Release(); }
            } while (!_disposed && revision != Volatile.Read(ref _audioRevision));
        }
        finally
        {
            Volatile.Write(ref _applyingAudio, 0);
            if (!_disposed && revision != Volatile.Read(ref _audioRevision)) StartAudioUpdate();
        }
    }

    private bool Current(Session session) => !_disposed && ReferenceEquals(_session, session) && session.Version == _version;
    private static PlaybackFailure SafeFailure(Exception error) => error is PlaybackException playback ? playback.Failure : PlaybackFailure.Unknown;
    private void Publish(PlaybackState state, PlaybackFailure failure = PlaybackFailure.None, Session? session = null, float percent = 0, bool willRetry = false, bool ended = false)
    {
        var value = new PlaybackSnapshot(++_sequence, state, failure, session?.Retries ?? 0, Math.Clamp(percent, 0, 100), session?.Software == true || session?.Options.Hardware == HardwareDecoding.Software, willRetry, ended);
        Volatile.Write(ref _snapshot, value); StateChanged?.Invoke(this, value);
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            _disposed = true; ++_version; _session?.Dispose(); _session = null;
            _disposeTask = DisposeCoreAsync(); return new(_disposeTask);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try { _backend.Signal -= BackendSignal; await _backend.DisposeAsync().ConfigureAwait(false); Publish(PlaybackState.Stopped); }
        finally { _serial.Release(); }
        await _progressMonitors.ConfigureAwait(false);
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private sealed class Session : IDisposable
    {
        private readonly CancellationTokenSource _cancel;
        private readonly object _timerSync = new();
        private CancellationTokenSource? _timeout;
        private bool _retired;
        public CancellationToken Token { get; }
        public CancellationTokenRegistration Registration;
        public long Version { get; }
        public Uri Source { get; }
        public PlaybackOptions Options { get; }
        public long Attempt;
        public int Retries;
        public bool Software;
        public bool HasPlayed;
        public long ResumePosition;
        public bool MonitorStarted, ProgressObserved, SilentStall, UserPaused;
        public PlaybackProgress? Progress;
        public long LastProgressAt;
        public long ContinuousProgressSince;
        public void ResetProgress() { Progress = null; ProgressObserved = false; SilentStall = false; ContinuousProgressSince = 0; }
        public Session(long version, Uri source, PlaybackOptions options, CancellationToken token)
        { Version = version; Source = source; Options = options; ResumePosition = options.StartPositionMilliseconds; _cancel = CancellationTokenSource.CreateLinkedTokenSource(token); Token = _cancel.Token; }
        public CancellationToken? RestartTimeout()
        {
            lock (_timerSync)
            {
                if (_retired) return null;
                _timeout?.Cancel(); _timeout?.Dispose();
                _timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
                return _timeout.Token;
            }
        }
        public void CancelTimeout() { lock (_timerSync) { if (!_retired) _timeout?.Cancel(); } }
        public void Dispose()
        {
            _cancel.Cancel(); Registration.Dispose();
            lock (_timerSync) { _retired = true; _timeout?.Cancel(); _timeout?.Dispose(); }
            _cancel.Dispose();
        }
        public override string ToString() => "Playback session";
    }
}
