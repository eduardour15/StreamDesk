using System.Collections.Concurrent;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

public sealed class PlaybackTests
{
    private static Uri Source(int id = 1) => new($"https://provider.invalid/live/fixture-user/fixture-password/{id}.ts");
    private static PlaybackService Service(ControlledBackend backend, int timeout = 3000, int delay = 10) =>
        new(backend, new(TimeSpan.FromMilliseconds(timeout), TimeSpan.FromMilliseconds(delay)));

    [Fact]
    public async Task StateTransitionsAudioAndStaleSignalsAreSafe()
    {
        var backend = new ControlledBackend(); await using var service = Service(backend);
        var states = new ConcurrentQueue<PlaybackState>(); service.StateChanged += (_, value) => states.Enqueue(value.State);
        await service.InitializeAsync();
        service.Volume = 500; service.IsMuted = true;
        service.Options = new(2500, HardwareDecoding.Software);
        await service.PlayAsync(Source());
        var first = backend.Opens.Last();
        Assert.Equal(PlaybackState.Loading, service.Snapshot.State);
        backend.Emit(new(first.Id, PlaybackSignalKind.Buffering, BufferPercent: 35));
        await UntilAsync(() => service.Snapshot.State == PlaybackState.Buffering);
        backend.Emit(new(first.Id, PlaybackSignalKind.Playing));
        await UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
        await service.InitializeAsync(); Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        Assert.Equal((100, true), backend.Audio);
        Assert.Equal(new PlaybackOptions(2500, HardwareDecoding.Software), first.Options);
        await service.PlayAsync(Source(2));
        backend.Emit(new(first.Id, PlaybackSignalKind.Failed, PlaybackFailure.Forbidden));
        await Task.Delay(30);
        Assert.Equal(PlaybackState.Loading, service.Snapshot.State);
        await service.StopAsync();
        Assert.Equal(PlaybackState.Stopped, service.Snapshot.State); Assert.False(service.HasActiveMedia);
        Assert.Contains(PlaybackState.Buffering, states); Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task TwentyRapidChannelChangesCancelPreviousAndNeverOverlapNativeOperations()
    {
        var backend = new ControlledBackend { OpeningDelay = 40 };
        await using var service = Service(backend);
        var tasks = Enumerable.Range(1, 20).Select(index => service.PlayAsync(Source(index))).ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, backend.Peak);
        Assert.EndsWith("/20.ts", backend.Opens.Last().Source.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(1, backend.Active);
        await service.StopAsync(); Assert.Equal(0, backend.Active);
    }

    [Theory]
    [InlineData(PlaybackFailure.Unauthorized)]
    [InlineData(PlaybackFailure.Forbidden)]
    [InlineData(PlaybackFailure.NotFound)]
    [InlineData(PlaybackFailure.Expired)]
    [InlineData(PlaybackFailure.ConnectionLimit)]
    [InlineData(PlaybackFailure.UnsupportedFormat)]
    [InlineData(PlaybackFailure.Unknown)]
    public async Task DefinitiveOrUnclassifiedFailuresAreNotAutomaticallyRetried(PlaybackFailure failure)
    {
        var backend = new ControlledBackend(); await using var service = Service(backend);
        await service.PlayAsync(Source());
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, failure));
        await UntilAsync(() => service.Snapshot.State == PlaybackState.Failed);
        await Task.Delay(60);
        Assert.Single(backend.Opens); Assert.Equal(failure, service.Snapshot.Failure); Assert.Equal(0, backend.Active);
        Assert.DoesNotContain("fixture-password", service.Snapshot.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("provider.invalid", service.Snapshot.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PlaybackFailure.Network)]
    [InlineData(PlaybackFailure.Timeout)]
    public async Task TransientRecoveryHasFiniteBudgetEvenIfPlayingBetweenFailures(PlaybackFailure failure)
    {
        var backend = new ControlledBackend(); await using var service = Service(backend);
        await service.PlayAsync(Source());
        for (var index = 1; index <= 3; index++)
        {
            var attempt = backend.Opens.Last().Id;
            backend.Emit(new(attempt, PlaybackSignalKind.Playing));
            await UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
            backend.Emit(new(attempt, PlaybackSignalKind.Failed, failure));
            if (index < 3) await UntilAsync(() => backend.Opens.Count > index);
        }
        await UntilAsync(() => service.Snapshot.State == PlaybackState.Failed);
        await Task.Delay(70);
        Assert.Equal(3, backend.Opens.Count); Assert.Equal(2, service.Snapshot.RetryCount); Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task HardwareFallbackIsOnceAndDoesNotApplyToDenial()
    {
        var backend = new ControlledBackend(); await using var service = Service(backend);
        await service.PlayAsync(Source());
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.HardwareDecoder));
        await UntilAsync(() => backend.Opens.Count == 2);
        Assert.Equal(HardwareDecoding.Software, backend.Opens.Last().Options.Hardware);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.HardwareDecoder));
        await UntilAsync(() => service.Snapshot.State == PlaybackState.Failed);
        await Task.Delay(50); Assert.Equal(2, backend.Opens.Count); Assert.True(service.Snapshot.UsingSoftware);
    }

    [Fact]
    public async Task WatchdogTimesOutAndStopCancelsBackoffAndOldEvents()
    {
        var backend = new ControlledBackend(); await using var service = Service(backend, timeout: 60, delay: 200);
        await service.PlayAsync(Source());
        var attempt = backend.Opens.Last().Id;
        await UntilAsync(() => service.Snapshot.Failure == PlaybackFailure.Timeout);
        await service.StopAsync();
        backend.Emit(new(attempt, PlaybackSignalKind.Playing));
        await Task.Delay(270);
        Assert.Single(backend.Opens); Assert.Equal(PlaybackState.Stopped, service.Snapshot.State);
    }

    [Fact]
    public async Task ClosingDuringOpeningAndBufferingDisposesOnceAndRejectsReuse()
    {
        var backend = new ControlledBackend { OpeningDelay = 100 };
        var service = Service(backend);
        var opening = service.PlayAsync(Source());
        await UntilAsync(() => backend.Opens.Count == 1);
        var close = service.DisposeAsync().AsTask();
        await Task.WhenAll(opening, close).WaitAsync(TimeSpan.FromSeconds(5));
        await service.DisposeAsync();
        Assert.Equal(1, backend.Disposals); Assert.Equal(0, backend.Active);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.PlayAsync(Source()));
        Assert.Null(service.PlayerHandle);
    }

    [Fact]
    public async Task CallerCancellationAfterOpenStopsMedia()
    {
        var backend = new ControlledBackend(); await using var service = Service(backend);
        using var token = new CancellationTokenSource();
        await service.PlayAsync(Source(), token.Token); token.Cancel();
        await UntilAsync(() => service.Snapshot.State == PlaybackState.Stopped);
        Assert.Equal(0, backend.Active);
    }

    [Theory]
    [InlineData("access", "HTTP 401 error", PlaybackFailure.Unauthorized)]
    [InlineData("http", "HTTP 403 error", PlaybackFailure.Forbidden)]
    [InlineData("http", "HTTP 404 error", PlaybackFailure.NotFound)]
    [InlineData("http", "HTTP 503 error", PlaybackFailure.Network)]
    [InlineData("http", "connection timed out", PlaybackFailure.Timeout)]
    [InlineData("d3d11va", "failed to create decoder", PlaybackFailure.HardwareDecoder)]
    [InlineData("http", "unknown failure", PlaybackFailure.None)]
    [InlineData("avcodec", "codec not supported", PlaybackFailure.UnsupportedFormat)]
    public void NativeDiagnosticsProduceOnlySafeFailureCodes(string module, string message, PlaybackFailure expected) =>
        Assert.Equal(expected, NativeFailureClassifier.Classify(module, message));

    [Theory]
    [InlineData(249)] [InlineData(10001)]
    public void InvalidBufferIsRejected(int milliseconds) => Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackOptions(milliseconds).Validate());

    internal static async Task UntilAsync(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready()) await Task.Delay(5, timeout.Token);
    }
}

internal sealed class ControlledBackend : IPlaybackBackend
{
    public record Opened(long Id, Uri Source, PlaybackOptions Options);
    public ConcurrentQueue<Opened> Opens { get; } = new();
    private bool _ready;
    private int _active, _peak;
    public int Active => Volatile.Read(ref _active);
    public int Peak => Volatile.Read(ref _peak);
    public int OpeningDelay { get; init; }
    public int Disposals { get; private set; }
    public (int, bool) Audio { get; private set; }
    public object? PlayerHandle => _ready ? this : null;
    public bool IsReady => _ready;
    public event EventHandler<PlaybackSignal>? Signal;
    public Task InitializeAsync(CancellationToken token) { _ready = true; return Task.CompletedTask; }
    public async Task OpenAsync(Uri source, PlaybackOptions options, long attemptId, CancellationToken token)
    {
        Assert.Equal(0, Active);
        var count = Interlocked.Increment(ref _active); Interlocked.Exchange(ref _peak, Math.Max(count, Peak));
        Opens.Enqueue(new(attemptId, source, options));
        if (OpeningDelay > 0) await Task.Delay(OpeningDelay, token);
    }
    public Task StopAsync() { Interlocked.Exchange(ref _active, 0); return Task.CompletedTask; }
    public Task SetAudioAsync(int volume, bool muted) { Audio = (volume, muted); return Task.CompletedTask; }
    public void Emit(PlaybackSignal value) => Signal?.Invoke(this, value);
    public ValueTask DisposeAsync() { Disposals++; _ready = false; Interlocked.Exchange(ref _active, 0); return ValueTask.CompletedTask; }
}
