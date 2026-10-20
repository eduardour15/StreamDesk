using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private PlaybackSnapshot _playback = new(0, PlaybackState.Idle);
    private string? _playingChannelId;
    private CancellationTokenSource? _channelSwitchCts;
    private string _osdText = string.Empty;
    private bool _showOsd;
    public string? PlayingChannelId => _playingChannelId;
    public string OsdText { get => _osdText; private set => SetProperty(ref _osdText, value); }
    public bool ShowOsd { get => _showOsd; set => SetProperty(ref _showOsd, value); }
    public string VolumeLabel => $"{Volume}%";
    public void TriggerOsd(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        OsdText = message;
        ShowOsd = true;
    }
    public string PlaybackStatus => _playback.Message + (_playback.UsingSoftware ? " · Modo compatible" : "") +
        (_playback.WillRetry && _playback.RetryCount is > 0 and <= 2 && _playback.Failure is PlaybackFailure.Network or PlaybackFailure.Timeout
            ? $" · Reintento {_playback.RetryCount}/2" : "");
    public bool ShowPlaybackPlaceholder => !_playback.IsActive;
    public bool ShowPlaybackCaption => !ShowPlaybackPlaceholder || _playback.State == PlaybackState.Failed || _playback.WillRetry;
    public bool CanRetryPlayback => _playback.State == PlaybackState.Failed && !_playback.WillRetry;
    public bool IsPlaying => _playback.State == PlaybackState.Playing;
    public bool IsPaused => _playback.State == PlaybackState.Paused;
    public bool CanStopPlayback => _playback.IsActive;
    public bool IsVideoLoading => !_disposed && _playback.State is PlaybackState.Loading or PlaybackState.Buffering;
    public bool HasVideoBufferPercent => _playback.State == PlaybackState.Buffering && _playback.BufferPercent is > 0f and < 100f;
    public int VideoBufferPercent => HasVideoBufferPercent ? (int)Math.Round(Math.Clamp(_playback.BufferPercent, 1f, 99f)) : 0;
    public bool IsVideoLoadingIndeterminate => IsVideoLoading && !HasVideoBufferPercent;
    public string VideoLoadingMessage => !IsVideoLoading
        ? string.Empty
        : _playback.State == PlaybackState.Loading
            ? "Abriendo transmisión…"
            : HasVideoBufferPercent
                ? $"Cargando búfer ({VideoBufferPercent}%)…"
                : "Cargando búfer…";
    public bool CanHideControls => IsPlaying && !IsWorking;
    public string NowPlaying => _playback.IsActive
        ? _mediaWatch?.Item.Name ?? _watch?.Channel.Name ?? CurrentPlaybackPrompt
        : ShowMediaCatalog ? CurrentPlaybackPrompt : _watch?.Channel.Name ?? "Elige un canal";
    private string CurrentPlaybackPrompt => ShowSeriesDetails
        ? SelectedEpisode?.Name ?? SelectedMedia?.Name ?? "Elige un episodio"
        : SelectedMedia?.Name ?? "Elige un título";
    public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";
    public Wpf.Ui.Controls.SymbolRegular PlayPauseSymbol => IsPlaying ? Wpf.Ui.Controls.SymbolRegular.Pause24 : Wpf.Ui.Controls.SymbolRegular.Play24;
    public string PlayPauseAction => IsPlaying ? "Pausar" : IsPaused ? "Reanudar" : "Reproducir";
    public string PlayPauseToolTip => IsPlaying ? "Pausar (Espacio)" : IsPaused ? "Reanudar (Espacio)" : "Reproducir (Espacio)";
    public string PlayActionIcon => _playback.IsActive ? "⏹" : "▶";
    public string PlayAction => _playback.IsActive ? "Detener" : "Reproducir";

    public async Task TogglePlayPauseAsync()
    {
        if (IsPlaying) { await _playbackService.PauseAsync(); TriggerOsd("En pausa"); }
        else if (IsPaused) { await _playbackService.ResumeAsync(); TriggerOsd("Reproduciendo"); }
        else if (ShowMediaBrowse || ShowMovieDetails || ShowSeriesDetails) await PlayMediaAsync();
        else await PlaySelectedAsync();
    }
    public async Task TogglePlaybackAsync() => await TogglePlayPauseAsync();
    public int Volume
    {
        get => _playbackService.Volume;
        set
        {
            _playbackService.Volume = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeLabel));
            TriggerOsd($"Volumen: {_playbackService.Volume}%");
        }
    }
    public bool IsMuted
    {
        get => _playbackService.IsMuted;
        set
        {
            _playbackService.IsMuted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MuteIcon));
            OnPropertyChanged(nameof(MuteSymbol)); TriggerOsd(_playbackService.IsMuted ? "Silenciado" : $"Sonido activo ({Volume}%)");
        }
    }
    public string MuteIcon => IsMuted ? "Mute" : "Sound"; public Wpf.Ui.Controls.SymbolRegular MuteSymbol => IsMuted ? Wpf.Ui.Controls.SymbolRegular.SpeakerMute24 : Wpf.Ui.Controls.SymbolRegular.Speaker224;
    public int MaxAmplificationDb => PlaybackService.MaxAmplificationDb;
    public int AmplificationDb
    {
        get => _playbackService.AmplificationDb;
        set
        {
            var clamped = Math.Clamp(value, 0, PlaybackService.MaxAmplificationDb);
            var changed = _playbackService.AmplificationDb != clamped;
            _playbackService.AmplificationDb = clamped;
            NotifyAmplificationState();
            if (changed)
            {
                TriggerOsd(clamped == 0 ? "Amplificación: 0 dB (Normal)" : $"Amplificación: +{clamped} dB");
                QueueAmplificationSave(clamped);
            }
        }
    }
    public bool IsAmplified => AmplificationDb > 0;
    public string AmplificationLabel => AmplificationDb <= 0 ? "0 dB (Normal)" : $"+{AmplificationDb} dB";
    public string AmplificationShortLabel => $"+{AmplificationDb} dB";
    public void CycleAmplification() => AmplificationDb = AmplificationDb >= PlaybackService.MaxAmplificationDb ? 0 : AmplificationDb + 2;
    private void ApplyProfileAmplification(int amplificationDb)
    {
        _playbackService.AmplificationDb = Math.Clamp(amplificationDb, 0, PlaybackService.MaxAmplificationDb);
        NotifyAmplificationState();
    }
    private void NotifyAmplificationState()
    {
        OnPropertyChanged(nameof(AmplificationDb));
        OnPropertyChanged(nameof(IsAmplified));
        OnPropertyChanged(nameof(AmplificationLabel));
        OnPropertyChanged(nameof(AmplificationShortLabel));
    }
    private void NotifyPlaybackLoadingState()
    {
        OnPropertyChanged(nameof(IsVideoLoading));
        OnPropertyChanged(nameof(HasVideoBufferPercent));
        OnPropertyChanged(nameof(VideoBufferPercent));
        OnPropertyChanged(nameof(IsVideoLoadingIndeterminate));
        OnPropertyChanged(nameof(VideoLoadingMessage));
    }
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
        _channelSwitchCts?.Cancel();
        _channelSwitchCts?.Dispose();
        _channelSwitchCts = null;
        await CaptureMediaAsync(true);
        _mediaWatch = null;
        _mediaDetails = MediaPlaybackDetails.Empty; NotifyMediaDetails();
        _playingChannelId = null;
        _watch = null;
        IsImmersiveMode = false;
        if (_playback.State is PlaybackState.Loading or PlaybackState.Buffering)
        {
            _playback = new(_playback.Sequence, PlaybackState.Stopped);
            NotifyPlaybackLoadingState();
        }
        OnPropertyChanged(nameof(PlayingChannelId));
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
            if (snapshot.State is PlaybackState.Loading or PlaybackState.Buffering)
            {
                if ((watch is not null && !ReferenceEquals(watch, _watch)) || (media is not null && !ReferenceEquals(media, _mediaWatch))) return;
            }
            _playback = snapshot;
            NotifyPlaybackLoadingState();
            StatusMessage = PlaybackStatus;
            OnPropertyChanged(nameof(PlaybackStatus)); OnPropertyChanged(nameof(ShowPlaybackPlaceholder)); OnPropertyChanged(nameof(ShowPlaybackCaption)); OnPropertyChanged(nameof(CanRetryPlayback));
            OnPropertyChanged(nameof(IsPlaying)); OnPropertyChanged(nameof(IsPaused)); OnPropertyChanged(nameof(CanStopPlayback)); OnPropertyChanged(nameof(CanHideControls));
            OnPropertyChanged(nameof(PlayPauseIcon)); OnPropertyChanged(nameof(PlayPauseSymbol)); OnPropertyChanged(nameof(PlayPauseAction)); OnPropertyChanged(nameof(PlayPauseToolTip));
            OnPropertyChanged(nameof(PlayAction)); OnPropertyChanged(nameof(PlayActionIcon));
            if (media is not null && ReferenceEquals(media, _mediaWatch) && snapshot.Sequence > media.AfterSequence)
            {
                if (snapshot.State == PlaybackState.Playing) media.Played = true;
                if (snapshot.EndOfMedia)
                {
                    media.Completed = true;
                    _progressWrites = SaveMediaProgressAsync(_progressWrites, media);
                    if (AutoRandomNextEpisode && media.Item.Kind == ContentKind.Episode && !media.RandomAdvanceTriggered && media.Version == _sessionVersion)
                    {
                        media.RandomAdvanceTriggered = true;
                        _autoAdvanceTask = AdvanceRandomEpisodeOnEndedAsync(media);
                    }
                }
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
