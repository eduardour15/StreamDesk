using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private PlaybackSnapshot _playback = new(0, PlaybackState.Idle);
    private string? _playingChannelId;
    public string PlaybackStatus => _playback.Message + (_playback.UsingSoftware ? " · Modo compatible" : "") +
        (_playback.WillRetry && _playback.RetryCount is > 0 and <= 2 && _playback.Failure is PlaybackFailure.Network or PlaybackFailure.Timeout
            ? $" · Reintento {_playback.RetryCount}/2" : "");
    public bool ShowPlaybackPlaceholder => !_playback.IsActive;
    public bool CanRetryPlayback => _playback.State == PlaybackState.Failed && !_playback.WillRetry;
    public bool IsPlaying => _playback.State == PlaybackState.Playing;
    public bool CanHideControls => IsPlaying && !IsWorking;
    public string NowPlaying => _mediaWatch?.Item.Name ?? _watch?.Channel.Name ?? "Elige un canal";
    public string PlayAction => _playback.IsActive ? "Detener" : "Reproducir";
    public async Task TogglePlaybackAsync() { if (_playback.IsActive) await StopPlaybackAsync(); else await PlaySelectedAsync(); }
    public int Volume { get => _playbackService.Volume; set { _playbackService.Volume = value; OnPropertyChanged(); } }
    public bool IsMuted { get => _playbackService.IsMuted; set { _playbackService.IsMuted = value; OnPropertyChanged(); } }
    public int BufferMilliseconds
    {
        get => _playbackService.Options.BufferMilliseconds;
        set { _playbackService.Options = (_playbackService.Options with { BufferMilliseconds = value }).Validate(); OnPropertyChanged(); }
    }
    public bool SoftwareDecoding
    {
        get => _playbackService.Options.Hardware == HardwareDecoding.Software;
        set { _playbackService.Options = _playbackService.Options with { Hardware = value ? HardwareDecoding.Software : HardwareDecoding.Automatic }; OnPropertyChanged(); }
    }

    public async Task InitializePlaybackAsync()
    {
        try
        {
            await _playbackService.InitializeAsync(_shutdown.Token);
            if (_disposed) return;
            IsPlaybackReady = _playbackService.IsReady;
            OnPropertyChanged(nameof(PlayerHandle));
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _logger.LogError("playback_init", exception);
            StatusMessage = new PlaybackException(PlaybackFailure.Unavailable).Message;
        }
    }

    public async Task StopPlaybackAsync()
    {
        await CaptureMediaAsync(true);
        _mediaWatch = null;
        _mediaDetails = MediaPlaybackDetails.Empty; NotifyMediaDetails();
        _playingChannelId = null;
        _watch = null;
        OnPropertyChanged(nameof(NowPlaying));
        try { await _playbackService.StopAsync(); }
        catch (Exception exception) { _logger.LogError("playback_stop", exception); }
    }

    public async Task NavigateChannelAsync(int direction)
    {
        if (ShowMediaCatalog || ShowContinueWatching) { await NavigateEpisodeAsync(direction); return; }
        if (_disposed || Channels.Count == 0 || direction is not (-1 or 1)) return;
        var currentId = _playingChannelId ?? SelectedChannel?.Id;
        var index = -1;
        for (var position = 0; position < Channels.Count; position++) if (Channels[position].Id == currentId) { index = position; break; }
        var next = index < 0 ? (direction > 0 ? 0 : Channels.Count - 1) : index + direction;
        if (next < 0 || next >= Channels.Count) return;
        SelectedChannel = Channels[next]; await PlaySelectedAsync();
    }

    public async Task SuspendPlaybackAsync()
    {
        await StopPlaybackAsync();
        void Apply() { if (!_disposed) StatusMessage = "Reproducción en pausa tras suspensión del equipo."; }
        if (_ui is not null && SynchronizationContext.Current != _ui) _ui.Post(_ => Apply(), null); else Apply();
    }

    public void ReportWindowError(Exception exception)
    { _logger.LogError("window_display", exception); StatusMessage = "No se pudo cambiar el modo de ventana."; }

    private void PlaybackStateChanged(object? sender, PlaybackSnapshot snapshot)
    {
        var watch = _watch;
        var media = _mediaWatch;
        void Apply()
        {
            if (_disposed || snapshot.Sequence <= _playback.Sequence) return;
            _playback = snapshot;
            StatusMessage = PlaybackStatus;
            OnPropertyChanged(nameof(PlaybackStatus)); OnPropertyChanged(nameof(ShowPlaybackPlaceholder)); OnPropertyChanged(nameof(CanRetryPlayback));
            OnPropertyChanged(nameof(IsPlaying)); OnPropertyChanged(nameof(CanHideControls)); OnPropertyChanged(nameof(PlayAction));
            if (media is not null && ReferenceEquals(media, _mediaWatch) && snapshot.Sequence > media.AfterSequence)
            {
                if (snapshot.State == PlaybackState.Playing) media.Played = true;
                if (snapshot.EndOfMedia) { media.Completed = true; _progressWrites = SaveMediaProgressAsync(_progressWrites, media); }
            }
            if (snapshot.State == PlaybackState.Playing && watch is not null && ReferenceEquals(watch, _watch) && snapshot.Sequence > watch.AfterSequence && !watch.Recorded)
            {
                watch.Recorded = true;
                watch.PlayedAt = DateTimeOffset.UtcNow;
                _historyWrites = RecordWatchAsync(_historyWrites, watch);
            }
        }
        if (_ui is not null && SynchronizationContext.Current != _ui) _ui.Post(_ => Apply(), null); else Apply();
    }
}
