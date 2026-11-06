using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    public sealed record QualityChoice(LiveQualityPreference Value, string Name);
    public IReadOnlyList<QualityChoice> LiveQualityChoices { get; } =
        [new(LiveQualityPreference.Automatic, "Automática"), new(LiveQualityPreference.DataSaver, "Ahorro (menor variante)")];

    public bool PrioritizeContinuity
    {
        get => _playbackService.Options.PrioritizeContinuity;
        set
        {
            if (value == PrioritizeContinuity) return;
            _playbackService.Options = _playbackService.Options with { PrioritizeContinuity = value };
            OnPropertyChanged(); _ = SavePreferencesAsync();
        }
    }
    public LiveQualityPreference LiveQuality
    {
        get => _playbackService.Options.LiveQuality;
        set
        {
            if (value == LiveQuality) return;
            _playbackService.Options = (_playbackService.Options with { LiveQuality = value }).Validate();
            OnPropertyChanged(); _ = SavePreferencesAsync();
        }
    }

    public async Task ApplyLivePreferencesAsync()
    {
        await SavePreferencesAsync();
        if (_disposed || _watch is not { } watch || _mediaWatch is not null || !(_playback.IsActive || _playback.State == PlaybackState.Failed)) return;
        if (LiveQuality == LiveQualityPreference.DataSaver)
            SelectedFormat = AvailableFormats.FirstOrDefault(format => format.Equals("m3u8", StringComparison.OrdinalIgnoreCase)) ?? SelectedFormat;
        await PlayChannelCoreAsync(watch.Channel, preserveAlternateFlag: false);
        TriggerOsd("Preferencias de fluidez aplicadas al canal");
    }

    private string _playbackDiagnosticsText = "Diagnóstico disponible al reproducir una señal.";
    public string PlaybackDiagnosticsText => _playbackDiagnosticsText;

    private bool _isPlaybackStatsOpen;
    public bool IsPlaybackStatsOpen
    {
        get => _isPlaybackStatsOpen;
        set => SetProperty(ref _isPlaybackStatsOpen, value);
    }

    public void TogglePlaybackStats()
    {
        IsPlaybackStatsOpen = !IsPlaybackStatsOpen;
        if (IsPlaybackStatsOpen)
        {
            IsAudioSubtitlesOpen = false;
            CloseEpisodesDrawer();
        }
    }
    public void ClosePlaybackStats() => IsPlaybackStatsOpen = false;

    public string StatsBitrateText => _playbackService.Diagnostics.ReceivedMegabitsPerSecond is { } rate ? $"{rate:0.00} Mb/s" : "Sin datos";
    public string StatsFpsText => _playbackService.Diagnostics.VideoFramesPerSecond is { } fps ? $"{fps:0.0} fps" : "Sin datos";
    public string StatsBufferText => $"{_playbackService.Diagnostics.EffectiveBufferMilliseconds / 1000d:0.0} s ({_playbackService.Diagnostics.EffectiveBufferMilliseconds} ms)";
    public string StatsRebufferText => $"{_playbackService.Diagnostics.RebufferCount} ({_playbackService.Diagnostics.WaitingSeconds:0} s)";
    public string StatsDecoderText => SoftwareDecoding ? "Software (CPU)" : "Hardware (GPU)";
    public string StatsFormatText => string.IsNullOrWhiteSpace(SelectedFormat) ? "Automático" : SelectedFormat.ToUpperInvariant();

    private void RefreshPlaybackDiagnostics()
    {
        var value = _playbackService.Diagnostics;
        var text = value.EffectiveBufferMilliseconds <= 0 ? "Diagnóstico disponible al reproducir una señal."
            : $"Recepción: {(value.ReceivedMegabitsPerSecond is { } rate ? $"{rate:0.00} Mb/s" : "sin datos")} · "
            + $"Vídeo: {(value.VideoFramesPerSecond is { } fps ? $"{fps:0.0} fps" : "sin datos")} · "
            + $"Búfer: {value.EffectiveBufferMilliseconds / 1000d:0.0} s · Recargas: {value.RebufferCount} ({value.WaitingSeconds:0} s)"
            + (value.AdaptiveLiveDelayMilliseconds > 0 ? $" · Margen HLS solicitado: {value.AdaptiveLiveDelayMilliseconds / 1000} s" : "")
            + (value.Observation == PlaybackObservation.DataWithoutVideoProgress ? " · Llegan datos sin avance de imagen" : "");
        if (_playbackDiagnosticsText != text)
        {
            _playbackDiagnosticsText = text;
            OnPropertyChanged(nameof(PlaybackDiagnosticsText));
        }
        OnPropertyChanged(nameof(StatsBitrateText));
        OnPropertyChanged(nameof(StatsFpsText));
        OnPropertyChanged(nameof(StatsBufferText));
        OnPropertyChanged(nameof(StatsRebufferText));
        OnPropertyChanged(nameof(StatsDecoderText));
        OnPropertyChanged(nameof(StatsFormatText));
    }
}
