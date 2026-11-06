using System.Diagnostics;
using System.Windows;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public partial class MainViewModel
{
    private bool _isNextEpisodeOverlayOpen;
    public bool IsNextEpisodeOverlayOpen
    {
        get => _isNextEpisodeOverlayOpen;
        set
        {
            if (SetProperty(ref _isNextEpisodeOverlayOpen, value))
            {
                OnPropertyChanged(nameof(IsNextEpisodeOverlayOpen));
            }
        }
    }

    private MediaItem? _nextEpisodeCandidate;
    public MediaItem? NextEpisodeCandidate
    {
        get => _nextEpisodeCandidate;
        private set
        {
            if (SetProperty(ref _nextEpisodeCandidate, value))
            {
                OnPropertyChanged(nameof(HasNextEpisodeAvailable));
            }
        }
    }

    public bool HasNextEpisodeAvailable => NextEpisodeCandidate is not null;

    private string _nextEpisodeSeasonEpisodeLabel = "";
    public string NextEpisodeSeasonEpisodeLabel
    {
        get => _nextEpisodeSeasonEpisodeLabel;
        private set => SetProperty(ref _nextEpisodeSeasonEpisodeLabel, value);
    }

    private string _nextEpisodeNameLabel = "";
    public string NextEpisodeNameLabel
    {
        get => _nextEpisodeNameLabel;
        private set => SetProperty(ref _nextEpisodeNameLabel, value);
    }

    private string _nextEpisodeCountdownLabel = "";
    public string NextEpisodeCountdownLabel
    {
        get => _nextEpisodeCountdownLabel;
        private set => SetProperty(ref _nextEpisodeCountdownLabel, value);
    }

    private int _nextEpisodeSecondsRemaining = 10;
    public int NextEpisodeSecondsRemaining
    {
        get => _nextEpisodeSecondsRemaining;
        private set => SetProperty(ref _nextEpisodeSecondsRemaining, value);
    }

    private double _nextEpisodeProgressValue = 10;
    public double NextEpisodeProgressValue
    {
        get => _nextEpisodeProgressValue;
        private set => SetProperty(ref _nextEpisodeProgressValue, value);
    }

    private CancellationTokenSource? _autoPlayCts;
    private Task? _autoPlayCountdownTask;

    public MediaItem? FindNextEpisode(MediaItem currentItem)
    {
        if (currentItem.Kind != ContentKind.Episode) return null;

        var allEpisodes = _episodes?.Items;
        if (allEpisodes is null || allEpisodes.Count == 0) return null;

        var seriesEpisodes = allEpisodes
            .Where(e => string.Equals(e.SeriesId, currentItem.SeriesId, StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrEmpty(currentItem.SeriesId))
            .OrderBy(e => e.Season)
            .ThenBy(e => e.Episode)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();

        if (seriesEpisodes.Count == 0) return null;

        var currentIndex = seriesEpisodes.FindIndex(e => e.StorageKey == currentItem.StorageKey || e.Id == currentItem.Id);
        if (currentIndex >= 0 && currentIndex + 1 < seriesEpisodes.Count)
        {
            return seriesEpisodes[currentIndex + 1];
        }

        // Fallback by Season and Episode numbers if exact storage key didn't match
        var candidate = seriesEpisodes.FirstOrDefault(e =>
            (e.Season == currentItem.Season && e.Episode > currentItem.Episode) ||
            (e.Season > currentItem.Season));

        return candidate;
    }

    private async Task TriggerNextEpisodeCountdownAsync(MediaWatch completedMedia)
    {
        if (completedMedia.Item.Kind != ContentKind.Episode || completedMedia.Version != _sessionVersion || _disposed) return;
        if (completedMedia.AutoPlayTriggered) return;
        completedMedia.AutoPlayTriggered = true;

        if (!string.IsNullOrWhiteSpace(completedMedia.Item.SeriesId) &&
            (_episodes is null || !_episodes.Items.Any(e => e.SeriesId == completedMedia.Item.SeriesId)))
        {
            await EnsureEpisodesLoadedForSeriesAsync(completedMedia.Item.SeriesId);
        }

        var next = FindNextEpisode(completedMedia.Item);
        if (next is null) return; // Last episode of last season; don't show auto-play

        NextEpisodeCandidate = next;
        NextEpisodeSeasonEpisodeLabel = $"Temporada {next.Season} · Episodio {next.Episode}";
        NextEpisodeNameLabel = string.IsNullOrWhiteSpace(next.Name) ? "" : next.Name;
        NextEpisodeSecondsRemaining = 10;
        NextEpisodeProgressValue = 10;
        NextEpisodeCountdownLabel = "Reproducción automática en 10 segundos...";
        IsNextEpisodeOverlayOpen = true;

        CancelCountdownTimer();
        _autoPlayCts = new CancellationTokenSource();
        _autoPlayCountdownTask = RunAutoPlayCountdownAsync(next, completedMedia, _autoPlayCts.Token);
    }

    private async Task RunAutoPlayCountdownAsync(MediaItem next, MediaWatch completedMedia, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        const double totalDuration = 10.0;

        try
        {
            while (stopwatch.Elapsed.TotalSeconds < totalDuration)
            {
                await Task.Delay(100, token);
                if (token.IsCancellationRequested || _disposed || completedMedia.Version != _sessionVersion) return;

                var elapsed = stopwatch.Elapsed.TotalSeconds;
                var remaining = Math.Max(0, (int)Math.Ceiling(totalDuration - elapsed));
                NextEpisodeSecondsRemaining = remaining;
                NextEpisodeProgressValue = Math.Max(0, totalDuration - elapsed);
                NextEpisodeCountdownLabel = $"Reproducción automática en {remaining} segundos...";
            }

            if (!token.IsCancellationRequested && !_disposed && completedMedia.Version == _sessionVersion)
            {
                IsNextEpisodeOverlayOpen = false;
                await PlayNextEpisodeNowAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            _logger.LogError("autoplay_countdown", error);
        }
    }

    public async Task PlayNextEpisodeNowAsync()
    {
        CancelCountdownTimer();
        IsNextEpisodeOverlayOpen = false;

        if (NextEpisodeCandidate is { } target)
        {
            NextEpisodeCandidate = null;
            SelectedEpisode = target;

            var targetSeason = Seasons.FirstOrDefault(s => s.Id == target.CategoryId || s.Name.Contains(target.Season.ToString()));
            if (targetSeason is not null && _season?.Id != targetSeason.Id)
            {
                _season = targetSeason;
                OnPropertyChanged(nameof(Season));
                OnPropertyChanged(nameof(Episodes));
            }

            await PlayMediaItemAsync(target);
        }
    }

    public void CancelNextEpisodeAutoPlay()
    {
        CancelCountdownTimer();
        IsNextEpisodeOverlayOpen = false;
        ContentStatus = "Reproducción automática cancelada.";
    }

    private void CancelCountdownTimer()
    {
        try
        {
            _autoPlayCts?.Cancel();
            _autoPlayCts?.Dispose();
        }
        catch { }
        finally
        {
            _autoPlayCts = null;
        }
    }

    public async Task EnsureEpisodesLoadedForSeriesAsync(string seriesId)
    {
        if (_client is not IContentProvider provider || _disposed || string.IsNullOrWhiteSpace(seriesId)) return;
        try
        {
            var cached = _contentStore is not null && _connectedProfileId is not null
                ? await _contentStore.LoadCatalogAsync(_connectedProfileId, ContentKind.Episode, seriesId, _session.Token)
                : null;
            var episodes = cached ?? await provider.GetEpisodesAsync(seriesId, _session.Token);
            if (!_disposed && (_episodes is null || !_episodes.Items.Any(e => e.SeriesId == seriesId)))
            {
                _episodes = episodes;
                OnPropertyChanged(nameof(Seasons));
                OnPropertyChanged(nameof(Episodes));
                OnPropertyChanged(nameof(SeriesSummary));
            }
        }
        catch (Exception error)
        {
            _logger.LogError("ensure_episodes", error);
        }
    }
}
