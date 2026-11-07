using System.Windows;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public partial class MainViewModel
{
    private IReadOnlyList<MediaTrackInfo> _formattedAudioTracks = [];
    public IReadOnlyList<MediaTrackInfo> FormattedAudioTracks
    {
        get => _formattedAudioTracks;
        private set
        {
            if (SetProperty(ref _formattedAudioTracks, value))
            {
                OnPropertyChanged(nameof(HasAudioTracks));
                OnPropertyChanged(nameof(HasTrackOptions));
                OnPropertyChanged(nameof(AudioStatusMessage));
            }
        }
    }

    private IReadOnlyList<MediaTrackInfo> _formattedSubtitleTracks = [];
    public IReadOnlyList<MediaTrackInfo> FormattedSubtitleTracks
    {
        get => _formattedSubtitleTracks;
        private set
        {
            if (SetProperty(ref _formattedSubtitleTracks, value))
            {
                OnPropertyChanged(nameof(HasSubtitleTracks));
                OnPropertyChanged(nameof(HasTrackOptions));
                OnPropertyChanged(nameof(SubtitleStatusMessage));
            }
        }
    }

    private MediaTrackInfo? _selectedAudioTrackInfo;
    public MediaTrackInfo? SelectedAudioTrackInfo
    {
        get => _selectedAudioTrackInfo;
        private set => SetProperty(ref _selectedAudioTrackInfo, value);
    }

    private MediaTrackInfo? _selectedSubtitleTrackInfo;
    public MediaTrackInfo? SelectedSubtitleTrackInfo
    {
        get => _selectedSubtitleTrackInfo;
        private set => SetProperty(ref _selectedSubtitleTrackInfo, value);
    }

    private bool _isAudioSubtitlesOpen;
    public bool IsAudioSubtitlesOpen
    {
        get => _isAudioSubtitlesOpen;
        set
        {
            if (SetProperty(ref _isAudioSubtitlesOpen, value))
            {
                OnPropertyChanged(nameof(IsAudioSubtitlesOpen));
            }
        }
    }

    private string _audioSubtitlesFeedback = "";
    public string AudioSubtitlesFeedback
    {
        get => _audioSubtitlesFeedback;
        set
        {
            if (SetProperty(ref _audioSubtitlesFeedback, value))
            {
                OnPropertyChanged(nameof(HasAudioSubtitlesFeedback));
            }
        }
    }

    public bool HasAudioSubtitlesFeedback => !string.IsNullOrWhiteSpace(_audioSubtitlesFeedback);

    public bool HasAudioTracks => FormattedAudioTracks.Count > 0;
    public bool HasTrackOptions => HasAudioTracks || HasSubtitleTracks;
    public bool HasNoAudioTracks => FormattedAudioTracks.Count == 0;
    public bool HasSubtitleTracks => FormattedSubtitleTracks.Any(t => !t.IsDisabled);
    public bool HasNoSubtitleTracks => !FormattedSubtitleTracks.Any(t => !t.IsDisabled);
    public string AudioStatusMessage => FormattedAudioTracks.Count == 0 ? "Cargando pistas de audio…" : (FormattedAudioTracks.Count == 1 ? "1 pista de audio disponible" : $"{FormattedAudioTracks.Count} pistas de audio");
    public string SubtitleStatusMessage => FormattedSubtitleTracks.Count <= 1 ? "Sin subtítulos disponibles" : $"{FormattedSubtitleTracks.Count(t => !t.IsDisabled)} pistas de subtítulos";

    public void ToggleAudioSubtitles()
    {
        IsAudioSubtitlesOpen = !IsAudioSubtitlesOpen;
        if (IsAudioSubtitlesOpen)
        {
            IsPlaybackStatsOpen = false;
            CloseEpisodesDrawer();
        }
    }
    public void CloseAudioSubtitles() => IsAudioSubtitlesOpen = false;

    public void UpdateFormattedTracks()
    {
        var rawAudio = _mediaDetails.AudioTracks;
        var currentAudioId = _mediaDetails.AudioTrack;

        var audioList = rawAudio.Select(track =>
        {
            var parsed = MediaTrackParser.ParseAudioTrack(track);
            return parsed with { IsSelected = parsed.Id == currentAudioId };
        }).ToList();

        FormattedAudioTracks = audioList;
        SelectedAudioTrackInfo = audioList.FirstOrDefault(t => t.Id == currentAudioId);

        var rawSubs = _mediaDetails.SubtitleTracks;
        var currentSubId = _mediaDetails.SubtitleTrack;

        var subList = new List<MediaTrackInfo>();
        var disabledTrack = new MediaTrackInfo(-1, "Disable", "Desactivados", "", IsDisabled: true)
        {
            IsSelected = currentSubId <= 0 || currentSubId == -1
        };
        subList.Add(disabledTrack);

        foreach (var track in rawSubs.Where(t => t.Id != -1))
        {
            var parsed = MediaTrackParser.ParseSubtitleTrack(track);
            subList.Add(parsed with { IsSelected = parsed.Id == currentSubId && currentSubId > 0 });
        }

        FormattedSubtitleTracks = subList;
        SelectedSubtitleTrackInfo = subList.FirstOrDefault(t => t.IsSelected) ?? disabledTrack;

        OnPropertyChanged(nameof(HasAudioTracks));
        OnPropertyChanged(nameof(HasNoAudioTracks));
        OnPropertyChanged(nameof(HasSubtitleTracks));
        OnPropertyChanged(nameof(HasTrackOptions));
        OnPropertyChanged(nameof(HasNoSubtitleTracks));
        OnPropertyChanged(nameof(AudioStatusMessage));
        OnPropertyChanged(nameof(SubtitleStatusMessage));
    }

    public async Task SelectAudioTrackInfoAsync(MediaTrackInfo trackInfo)
    {
        if (trackInfo is null) return;
        try
        {
            AudioSubtitlesFeedback = $"Aplicando audio: {trackInfo.DisplayName}…";
            if (await _playbackService.SelectTrackAsync(false, trackInfo.Id, _session.Token))
            {
                _mediaDetails = _mediaDetails with { AudioTrack = trackInfo.Id };
                SelectedAudioTrack = AudioTracks.FirstOrDefault(t => t.Id == trackInfo.Id);
                UpdateFormattedTracks();
                AudioSubtitlesFeedback = $"Audio seleccionado: {trackInfo.DisplayName}";
                TriggerOsd($"Audio: {trackInfo.DisplayName}");

                var watch = _mediaWatch;
                if (watch is not null && _contentStore is not null && _connectedProfileId is not null)
                {
                    var contentId = watch.Item.Kind == ContentKind.Episode
                        ? (watch.Item.SeriesId ?? watch.Item.CategoryId ?? watch.Item.Id)
                        : watch.Item.Id;

                    var existing = watch.ContentTrackPreference;
                    var updated = new MediaTrackPreference(
                        contentId,
                        watch.Item.Kind,
                        trackInfo.LanguageCode,
                        trackInfo.Region,
                        trackInfo.DisplayName,
                        existing?.SubtitleLanguage,
                        existing?.SubtitleRegion,
                        existing?.SubtitleTitle,
                        existing?.SubtitlesDisabled ?? false,
                        DateTimeOffset.UtcNow);

                    watch.ContentTrackPreference = updated;
                    await _contentStore.SaveTrackPreferenceAsync(_connectedProfileId, updated);
                }
            }
            else
            {
                AudioSubtitlesFeedback = "No se pudo cambiar la pista de audio.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            _logger.LogError("select_audio_track", error);
            AudioSubtitlesFeedback = "Error al cambiar pista de audio.";
        }
    }

    public async Task SelectSubtitleTrackInfoAsync(MediaTrackInfo trackInfo)
    {
        if (trackInfo is null) return;
        try
        {
            AudioSubtitlesFeedback = trackInfo.IsDisabled ? "Desactivando subtítulos…" : $"Aplicando subtítulos: {trackInfo.DisplayName}…";
            if (await _playbackService.SelectTrackAsync(true, trackInfo.Id, _session.Token))
            {
                _mediaDetails = _mediaDetails with { SubtitleTrack = trackInfo.Id };
                SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(t => t.Id == trackInfo.Id);
                UpdateFormattedTracks();
                AudioSubtitlesFeedback = trackInfo.IsDisabled ? "Subtítulos desactivados" : $"Subtítulos: {trackInfo.DisplayName}";
                TriggerOsd(trackInfo.IsDisabled ? "Subtítulos: Desactivados" : $"Subtítulos: {trackInfo.DisplayName}");

                var watch = _mediaWatch;
                if (watch is not null && _contentStore is not null && _connectedProfileId is not null)
                {
                    var contentId = watch.Item.Kind == ContentKind.Episode
                        ? (watch.Item.SeriesId ?? watch.Item.CategoryId ?? watch.Item.Id)
                        : watch.Item.Id;

                    var existing = watch.ContentTrackPreference;
                    var updated = new MediaTrackPreference(
                        contentId,
                        watch.Item.Kind,
                        existing?.AudioLanguage,
                        existing?.AudioRegion,
                        existing?.AudioTitle,
                        trackInfo.LanguageCode,
                        trackInfo.Region,
                        trackInfo.DisplayName,
                        trackInfo.IsDisabled,
                        DateTimeOffset.UtcNow);

                    watch.ContentTrackPreference = updated;
                    await _contentStore.SaveTrackPreferenceAsync(_connectedProfileId, updated);
                }
            }
            else
            {
                AudioSubtitlesFeedback = "No se pudo cambiar la pista de subtítulos.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            _logger.LogError("select_subtitle_track", error);
            AudioSubtitlesFeedback = "Error al cambiar subtítulos.";
        }
    }

    private async Task ApplyTrackPreferencesOnPlaybackStartedAsync(MediaWatch watch)
    {
        if (watch is null || _disposed || watch.Version != _sessionVersion) return;
        try
        {
            var pref = watch.ContentTrackPreference;
            var fallbackLang = (SelectedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) || SelectedLanguage.Contains("English", StringComparison.OrdinalIgnoreCase)) ? "en" : "es";

            // 1. Audio matching
            var bestAudio = MediaTrackParser.FindBestAudioMatch(FormattedAudioTracks, pref, fallbackLang);
            if (bestAudio is not null && bestAudio.Id != _mediaDetails.AudioTrack)
            {
                if (await _playbackService.SelectTrackAsync(false, bestAudio.Id, _session.Token))
                {
                    _mediaDetails = _mediaDetails with { AudioTrack = bestAudio.Id };
                    SelectedAudioTrack = AudioTracks.FirstOrDefault(t => t.Id == bestAudio.Id);
                }
            }

            // 2. Subtitle matching
            if (pref is not null)
            {
                var bestSub = MediaTrackParser.FindBestSubtitleMatch(FormattedSubtitleTracks, pref);
                if (bestSub is not null && bestSub.Id != _mediaDetails.SubtitleTrack)
                {
                    if (await _playbackService.SelectTrackAsync(true, bestSub.Id, _session.Token))
                    {
                        _mediaDetails = _mediaDetails with { SubtitleTrack = bestSub.Id };
                        SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(t => t.Id == bestSub.Id);
                    }
                }
            }

            UpdateFormattedTracks();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            _logger.LogError("apply_track_preferences", error);
        }
    }
}
