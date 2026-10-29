using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Tests;

public sealed class PlaybackStabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeStatusChatterCannotHideFrozenOutputOrCancelItsRecovery(bool bufferingOnly)
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(15), TimeSpan.Zero), clock);
        await StartAsync(service, backend);
        await SampleAsync(service, backend, clock, 10);
        await SampleAsync(service, backend, clock, 20);
        var attempt = backend.Opens.Last().Id;
        for (var second = 0; second < 4; second++)
        {
            backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 40));
            if (!bufferingOnly)
            {
                backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 100));
                backend.Emit(new(attempt, PlaybackSignalKind.Playing));
            }
            await service.ReadDetailsAsync();
            await SampleAsync(service, backend, clock, 20);
        }
        Assert.Equal(PlaybackState.Buffering, service.Snapshot.State);
        // Once frozen output is confirmed, Playing events alone must not cancel the watchdog.
        for (var second = 0; second < 5; second++)
        {
            backend.Emit(new(attempt, PlaybackSignalKind.Playing));
            await service.ReadDetailsAsync();
            clock.Advance(TimeSpan.FromSeconds(1));
            await service.ReadDetailsAsync();
        }
        await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
        Assert.Equal(PlaybackFailure.None, service.Snapshot.Failure);
        Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task RetryWatchdogAllowsTheActualLargerBufferToRefill()
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(15), TimeSpan.Zero), clock);
        await StartAsync(service, backend);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));
        await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await service.ReadDetailsAsync();
        Assert.Equal(2500, backend.Opens.Last().Options.BufferMilliseconds);
        await SampleAsync(service, backend, clock, 10);
        await SampleAsync(service, backend, clock, 20);
        await SampleAsync(service, backend, clock, 20, 4);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        await SampleAsync(service, backend, clock, 20);
        Assert.Equal(PlaybackState.Buffering, service.Snapshot.State);
        // A short hiccup which recovers itself must not open another connection.
        await SampleAsync(service, backend, clock, 30);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        Assert.Equal(2, backend.Opens.Count); Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task StableRetryResetKeepsTheLearnedBufferAndNeverExceedsItsLimit()
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(15), TimeSpan.Zero), clock);
        service.Options = new(3500);
        await StartAsync(service, backend);
        foreach (var expectedBuffer in new[] { 5000, 6500, 8000, 9500, 10000, 10000 })
        {
            var count = backend.Opens.Count;
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));
            await PlaybackTests.UntilAsync(() => backend.Opens.Count > count);
            Assert.Equal(expectedBuffer, backend.Opens.Last().Options.BufferMilliseconds);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await service.ReadDetailsAsync();
            await SampleAsync(service, backend, clock, 10);
            await SampleAsync(service, backend, clock, 20);
            await SampleAsync(service, backend, clock, 100, 61);
            Assert.Equal(0, service.Snapshot.RetryCount);
        }
        // Learning is limited to this channel session; preferences and VOD remain unchanged.
        Assert.Equal(3500, service.Options.BufferMilliseconds);
        service.Options = new(3500, OnDemand: true);
        await service.PlayAsync(new("https://provider.invalid/movie.mp4"));
        var beforeRetry = backend.Opens.Count;
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));
        await PlaybackTests.UntilAsync(() => backend.Opens.Count > beforeRetry);
        Assert.Equal(3500, backend.Opens.Last().Options.BufferMilliseconds);
        Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task RepeatedBriefFreezesDoNotCountAsContinuousHealthyPlayback()
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        var policy = new PlaybackPolicy(TimeSpan.FromSeconds(15), TimeSpan.Zero) { StableResetThreshold = TimeSpan.FromSeconds(6) };
        await using var service = new PlaybackService(backend, policy, clock);
        await StartAsync(service, backend);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));
        await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await service.ReadDetailsAsync();
        await SampleAsync(service, backend, clock, 10);
        await SampleAsync(service, backend, clock, 20);
        await SampleAsync(service, backend, clock, 20, 3);
        await SampleAsync(service, backend, clock, 30);
        await SampleAsync(service, backend, clock, 30, 3);
        await SampleAsync(service, backend, clock, 40);
        Assert.Equal(1, service.Snapshot.RetryCount);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
    }

    private static async Task StartAsync(PlaybackService service, ControlledBackend backend)
    {
        await service.PlayAsync(new("https://provider.invalid/live.ts"));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await service.ReadDetailsAsync();
    }

    private static async Task SampleAsync(PlaybackService service, ControlledBackend backend, ManualPlaybackClock clock, int frames, int seconds = 1)
    {
        backend.Progress = new(2000, frames, 0); var reads = backend.ProgressReads;
        clock.Advance(TimeSpan.FromSeconds(seconds));
        await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads);
        await service.ReadDetailsAsync();
    }
}
