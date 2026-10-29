using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed partial class PlaybackService
{
    private PlaybackDiagnostics _diagnostics = PlaybackDiagnostics.Empty;
    public PlaybackDiagnostics Diagnostics => Volatile.Read(ref _diagnostics);

    private static int AdaptiveDelay(Session session) => !session.Options.OnDemand && session.Source.Scheme is "http" or "https"
        && session.Source.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            ? (session.Options with { BufferMilliseconds = session.EffectiveBufferMilliseconds }).AdaptiveLiveDelayMilliseconds : 0;

    private TimeSpan NativeBufferTimeout(Session session) => TimeSpan.FromMilliseconds(
        Math.Max(_policy.StartupTimeout.TotalMilliseconds, AdaptiveDelay(session) > 0 ? AdaptiveDelay(session) + 5000 : 0));

    private void EndWaiting(Session session)
    {
        if (session.WaitingSince is { } started)
            session.WaitingSeconds += _clock.GetElapsedTime(started).TotalSeconds;
        session.WaitingSince = null;
    }

    private void ObservePlaybackState(Session? session, PlaybackState state, bool willRetry)
    {
        if (session is null || !Current(session) || state is PlaybackState.Stopped or PlaybackState.Idle)
        { Volatile.Write(ref _diagnostics, PlaybackDiagnostics.Empty); return; }
        if (session.PlayedOnce && (state == PlaybackState.Buffering || state == PlaybackState.Failed && willRetry) && session.WaitingSince is null)
        { session.RebufferCount++; session.WaitingSince = _clock.GetTimestamp(); }
        if (state == PlaybackState.Playing) session.PlayedOnce = true;
        if (state == PlaybackState.Paused || state == PlaybackState.Failed && !willRetry)
        { EndWaiting(session); session.MetricsSample = null; }
        var observation = state switch
        {
            PlaybackState.Paused => PlaybackObservation.Paused,
            PlaybackState.Buffering or PlaybackState.Failed => PlaybackObservation.Waiting,
            _ => PlaybackObservation.Unknown
        };
        Volatile.Write(ref _diagnostics, Diagnostics with
        {
            EffectiveBufferMilliseconds = session.EffectiveBufferMilliseconds,
            AdaptiveLiveDelayMilliseconds = AdaptiveDelay(session),
            ReceivedMegabitsPerSecond = state == PlaybackState.Playing ? Diagnostics.ReceivedMegabitsPerSecond : null,
            VideoFramesPerSecond = state == PlaybackState.Playing ? Diagnostics.VideoFramesPerSecond : null,
            RebufferCount = session.RebufferCount, WaitingSeconds = WaitingDuration(session),
            Observation = state == PlaybackState.Playing ? Diagnostics.Observation : observation
        });
    }

    private double WaitingDuration(Session session) => session.WaitingSeconds
        + (session.WaitingSince is { } started ? _clock.GetElapsedTime(started).TotalSeconds : 0);

    private void ObserveProgress(Session session, PlaybackProgress? sample)
    {
        var previous = session.MetricsSample;
        var now = _clock.GetTimestamp();
        var seconds = _clock.GetElapsedTime(session.MetricsSampleAt, now).TotalSeconds;
        session.MetricsSample = sample; session.MetricsSampleAt = now;
        double? rate = null, fps = null;
        if (sample is not null && previous is not null && seconds > 0)
        {
            if (sample.ReceivedBytes is { } bytes && previous.ReceivedBytes is { } oldBytes && bytes >= oldBytes)
                rate = (bytes - oldBytes) * 8d / seconds / 1_000_000;
            if (sample.VideoFrames > 0 && previous.VideoFrames > 0 && sample.VideoFrames >= previous.VideoFrames)
                fps = (sample.VideoFrames - previous.VideoFrames) / seconds;
        }
        var advanced = sample is not null && previous is not null && (sample.VideoFrames > 0 || previous.VideoFrames > 0
            ? sample.VideoFrames != previous.VideoFrames
            : sample.AudioBlocks > 0 || previous.AudioBlocks > 0 ? sample.AudioBlocks != previous.AudioBlocks
            : sample.PositionMilliseconds > 0 && sample.PositionMilliseconds != previous.PositionMilliseconds);
        if (advanced) EndWaiting(session);
        var observation = advanced ? PlaybackObservation.Progressing
            : session.SilentStall && rate > 0 && sample?.VideoFrames > 0 ? PlaybackObservation.DataWithoutVideoProgress
            : session.WaitingSince.HasValue ? PlaybackObservation.Waiting : PlaybackObservation.Unknown;
        Volatile.Write(ref _diagnostics, new(session.EffectiveBufferMilliseconds, AdaptiveDelay(session), rate, fps,
            Math.Max(0, sample?.LostVideoFrames ?? 0), session.RebufferCount, WaitingDuration(session), observation));
    }
}
