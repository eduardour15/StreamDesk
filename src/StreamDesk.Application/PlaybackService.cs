using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed class PlaybackService : IPlaybackService
{
    private readonly IPlaybackBackend _backend;
    private readonly PlaybackPolicy _policy;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly object _sync = new();
    private Session? _session;
    private long _version, _attempt, _sequence;
    private bool _disposed;
    private Task? _disposeTask;
    private int _volume = 75;
    private bool _muted;
    private PlaybackOptions _options = new();
    private PlaybackSnapshot _snapshot = new(0, PlaybackState.Idle);

    public PlaybackService(IPlaybackBackend backend, PlaybackPolicy? policy = null)
    {
        _backend = backend; _policy = policy ?? PlaybackPolicy.Default;
        if (_policy.StartupTimeout <= TimeSpan.Zero || _policy.RetryDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(policy));
        _backend.Signal += BackendSignal;
    }
    public object? PlayerHandle => _backend.PlayerHandle;
    public bool IsReady => !_disposed && _backend.IsReady;
    public bool HasActiveMedia => Snapshot.IsActive;
    public PlaybackSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public PlaybackOptions Options { get { lock (_sync) return _options; } set { value.Validate(); lock (_sync) { ObjectDisposedException.ThrowIf(_disposed, this); _options = value; } } }
    public int Volume { get => Volatile.Read(ref _volume); set { Volatile.Write(ref _volume, Math.Clamp(value, 0, 100)); _ = ApplyAudioAsync(); } }
    public bool IsMuted { get => Volatile.Read(ref _muted); set { Volatile.Write(ref _muted, value); _ = ApplyAudioAsync(); } }
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

    private async Task InitializeCoreAsync(CancellationToken token)
    {
        if (_backend.IsReady) return;
        try { await _backend.InitializeAsync(token).ConfigureAwait(false); await _backend.SetAudioAsync(Volume, IsMuted).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { Publish(PlaybackState.Failed, PlaybackFailure.Unavailable); throw new PlaybackException(PlaybackFailure.Unavailable); }
    }

    private async Task OpenAsync(Session session)
    {
        session.Token.ThrowIfCancellationRequested();
        session.HasPlayed = false;
        session.Attempt = ++_attempt;
        Publish(PlaybackState.Loading, session: session);
        await _backend.SetAudioAsync(Volume, IsMuted).ConfigureAwait(false);
        await _backend.OpenAsync(session.Source, session.Options with { Hardware = session.Software ? HardwareDecoding.Software : session.Options.Hardware }, session.Attempt, session.Token).ConfigureAwait(false);
        ArmTimeout(session);
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
                    session.HasPlayed = true; session.CancelTimeout(); Publish(PlaybackState.Playing, session: session); break;
                case PlaybackSignalKind.Buffering:
                    if (signal.BufferPercent >= 100 && session.HasPlayed) { session.CancelTimeout(); Publish(PlaybackState.Playing, session: session); }
                    else { if (Snapshot.State == PlaybackState.Playing) ArmTimeout(session); Publish(PlaybackState.Buffering, session: session, percent: signal.BufferPercent); }
                    break;
                case PlaybackSignalKind.Ended:
                    await FailAsync(session, PlaybackFailure.Network).ConfigureAwait(false); break;
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
        await _backend.StopAsync().ConfigureAwait(false);
        if (!Current(session) || session.Token.IsCancellationRequested) return;
        var hardwareFallback = failure == PlaybackFailure.HardwareDecoder && !session.Software && session.Options.Hardware == HardwareDecoding.Automatic;
        var retry = failure is PlaybackFailure.Network or PlaybackFailure.Timeout && session.Retries < 2;
        if (hardwareFallback) session.Software = true;
        else if (retry) session.Retries++;
        Publish(PlaybackState.Failed, failure, session, willRetry: hardwareFallback || retry);
        if (hardwareFallback || retry) _ = RecoverAsync(session, hardwareFallback ? TimeSpan.Zero : _policy.RetryDelay * session.Retries);
    }

    private async Task RecoverAsync(Session session, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, session.Token).ConfigureAwait(false);
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

    private void ArmTimeout(Session session)
    {
        var token = session.RestartTimeout();
        if (token.HasValue) _ = WatchTimeoutAsync(session.Attempt, token.Value);
    }
    private async Task WatchTimeoutAsync(long attempt, CancellationToken token)
    {
        try { await Task.Delay(_policy.StartupTimeout, token).ConfigureAwait(false); await ProcessSignalAsync(new(attempt, PlaybackSignalKind.Failed, PlaybackFailure.Timeout)).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private async Task CancelSessionAsync(Session session)
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try { if (!Current(session)) return; session.Attempt = 0; await _backend.StopAsync().ConfigureAwait(false); Publish(PlaybackState.Stopped, PlaybackFailure.Cancelled); }
        catch { if (Current(session)) Publish(PlaybackState.Failed, PlaybackFailure.Unknown); }
        finally { _serial.Release(); }
    }

    private async Task ApplyAudioAsync()
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try { if (!_disposed && _backend.IsReady) await _backend.SetAudioAsync(Volume, IsMuted).ConfigureAwait(false); }
        catch { /* Audio controls must not terminate an otherwise valid stream. */ }
        finally { _serial.Release(); }
    }

    private bool Current(Session session) => !_disposed && ReferenceEquals(_session, session) && session.Version == _version;
    private static PlaybackFailure SafeFailure(Exception error) => error is PlaybackException playback ? playback.Failure : PlaybackFailure.Unknown;
    private void Publish(PlaybackState state, PlaybackFailure failure = PlaybackFailure.None, Session? session = null, float percent = 0, bool willRetry = false)
    {
        var value = new PlaybackSnapshot(++_sequence, state, failure, session?.Retries ?? 0, Math.Clamp(percent, 0, 100), session?.Software == true || session?.Options.Hardware == HardwareDecoding.Software, willRetry);
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
        public Session(long version, Uri source, PlaybackOptions options, CancellationToken token)
        { Version = version; Source = source; Options = options; _cancel = CancellationTokenSource.CreateLinkedTokenSource(token); Token = _cancel.Token; }
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
