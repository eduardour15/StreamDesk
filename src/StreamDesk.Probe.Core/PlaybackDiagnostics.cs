namespace StreamDesk.Probe.Core;

public enum PlaybackObservation { Unknown, Progressing, Waiting, DataWithoutVideoProgress, Paused }

// Numeric, ephemeral observations only: never include source URLs or account metadata.
public sealed record PlaybackDiagnostics(int EffectiveBufferMilliseconds = 0, int AdaptiveLiveDelayMilliseconds = 0,
    double? ReceivedMegabitsPerSecond = null, double? VideoFramesPerSecond = null, long LostVideoFrames = 0,
    int RebufferCount = 0, double WaitingSeconds = 0, PlaybackObservation Observation = PlaybackObservation.Unknown)
{
    public static PlaybackDiagnostics Empty { get; } = new();
}
