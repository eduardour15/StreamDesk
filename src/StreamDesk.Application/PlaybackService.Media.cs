using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public sealed partial class PlaybackService
{
    public async Task<MediaPlaybackDetails> ReadDetailsAsync(CancellationToken cancellationToken = default)
    {
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return !_disposed && Snapshot.IsActive ? await _backend.ReadDetailsAsync(cancellationToken).ConfigureAwait(false) : MediaPlaybackDetails.Empty; }
        finally { _serial.Release(); }
    }
    public async Task<bool> SeekAsync(long milliseconds, CancellationToken cancellationToken = default)
    {
        var session = _session;
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (session is null || !Current(session) || !Snapshot.IsActive) return false;
            var details = await _backend.ReadDetailsAsync(cancellationToken).ConfigureAwait(false);
            return details.CanSeek && details.LengthMilliseconds > 0 && await _backend.SeekAsync(Math.Clamp(milliseconds, 0, details.LengthMilliseconds), cancellationToken).ConfigureAwait(false);
        }
        finally { _serial.Release(); }
    }
    public async Task<bool> SelectTrackAsync(bool subtitles, int id, CancellationToken cancellationToken = default)
    {
        var session = _session;
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (session is null || !Current(session) || !Snapshot.IsActive) return false;
            var details = await _backend.ReadDetailsAsync(cancellationToken).ConfigureAwait(false);
            return (subtitles ? details.SubtitleTracks : details.AudioTracks).Any(item => item.Id == id)
                && await _backend.SelectTrackAsync(subtitles, id, cancellationToken).ConfigureAwait(false);
        }
        finally { _serial.Release(); }
    }
}
