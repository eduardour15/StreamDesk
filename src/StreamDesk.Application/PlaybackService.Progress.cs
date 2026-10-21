using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed partial class PlaybackService
{
    private async Task MonitorProgressAsync(Session session)
    {
        try
        {
            using var timer = new PeriodicTimer(_policy.ProgressInterval, _clock);
            while (await timer.WaitForNextTickAsync(session.Token).ConfigureAwait(false))
            {
                await _serial.WaitAsync(session.Token).ConfigureAwait(false);
                try
                {
                    if (!Current(session)) return;
                    if (session.Attempt == 0 && !Snapshot.WillRetry) return;
                    if (Snapshot.State == PlaybackState.Buffering && !session.SilentStall)
                    {
                        // Preserve Progress and ProgressObserved across native Buffering events so a stream
                        // that exits Buffering(100%) with frozen frames is still detected as stalled.
                        session.LastProgressAt = _clock.GetTimestamp();
                        session.ContinuousProgressSince = 0;
                        continue;
                    }
                    if (Snapshot.State != PlaybackState.Playing && !session.SilentStall)
                    { session.ResetProgress(); continue; }
                    var sample = await _backend.ReadProgressAsync(session.Token).ConfigureAwait(false);
                    // Stop/Play can retire the session while the native read is in flight.
                    if (!Current(session) || session.Token.IsCancellationRequested) return;
                    // Missing clocks/statistics are unknown, never evidence of a frozen stream.
                    if (sample is null || (sample.PositionMilliseconds <= 0 && sample.VideoFrames <= 0 && sample.AudioBlocks <= 0))
                    { session.Progress = null; session.ProgressObserved = false; session.ContinuousProgressSince = 0; continue; }
                    var previous = session.Progress;
                    session.Progress = sample;
                    var now = _clock.GetTimestamp();
                    if (previous is null) { session.LastProgressAt = now; continue; }
                    // Prefer displayed/decoded video counters, including static scenes. Audio-only
                    // streams use audio counters, then the media clock if stats are unavailable.
                    var changed = sample.VideoFrames > 0 || previous.VideoFrames > 0
                        ? sample.VideoFrames != previous.VideoFrames
                        : sample.AudioBlocks > 0 || previous.AudioBlocks > 0
                            ? sample.AudioBlocks != previous.AudioBlocks
                            : sample.PositionMilliseconds != previous.PositionMilliseconds;
                    if (changed)
                    {
                        if (!session.ProgressObserved || session.SilentStall || session.ContinuousProgressSince == 0)
                            session.ContinuousProgressSince = now;
                        session.LastProgressAt = now; session.ProgressObserved = true;
                        if (session.SilentStall)
                        {
                            session.SilentStall = false; session.CancelTimeout();
                            Publish(PlaybackState.Playing, session: session);
                        }
                        else if (session.Retries > 0 && _clock.GetElapsedTime(session.ContinuousProgressSince, now) >= _policy.StableResetThreshold)
                        {
                            // Reset the transient retry budget after sustained healthy playback so long
                            // live TV sessions can survive isolated network hiccups hours apart.
                            session.Retries = 0;
                            Publish(PlaybackState.Playing, session: session);
                        }
                        continue;
                    }
                    // Allow both the configured cache and short scheduling/network pauses.
                    var tolerance = TimeSpan.FromMilliseconds(Math.Max(_policy.StallThreshold.TotalMilliseconds, session.Options.BufferMilliseconds * 2L));
                    if (!session.ProgressObserved || session.SilentStall || _clock.GetElapsedTime(session.LastProgressAt, now) < tolerance) continue;
                    // Natural VOD completion belongs to Ended; do not reconnect near its end.
                    if (session.Options.OnDemand && sample.LengthMilliseconds > 0 && sample.PositionMilliseconds >= sample.LengthMilliseconds - 1000) continue;
                    session.SilentStall = true;
                    session.ContinuousProgressSince = 0;
                    Publish(PlaybackState.Buffering, session: session);
                    ArmTimeout(session, _policy.StallTimeout);
                }
                catch (OperationCanceledException) when (session.Token.IsCancellationRequested) { return; }
                catch { session.Progress = null; session.ProgressObserved = false; session.ContinuousProgressSince = 0; }
                finally { _serial.Release(); }
            }
        }
        catch (OperationCanceledException) { }
    }
}
