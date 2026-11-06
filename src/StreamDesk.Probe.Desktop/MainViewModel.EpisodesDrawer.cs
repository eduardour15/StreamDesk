using System.Globalization;
using System.Text.RegularExpressions;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed record DrawerSeasonItem(
    XtreamCategory Category,
    int SeasonNumber,
    string Name,
    int EpisodeCount,
    string EpisodeCountLabel,
    bool IsSelected);

public sealed record DrawerEpisodeItem(
    MediaItem Episode,
    int Number,
    string Title,
    string DisplayTitle,
    string? PosterUrl,
    string Description,
    string DurationText,
    double ProgressFraction,
    bool HasProgress,
    bool IsPlaying,
    string PlayStatusText);

public sealed partial class MainViewModel
{
    public bool CanBrowseEpisodes => _mediaWatch?.Item.Kind == ContentKind.Episode;

    private bool _isEpisodesDrawerOpen;
    public bool IsEpisodesDrawerOpen
    {
        get => _isEpisodesDrawerOpen;
        set
        {
            if (SetProperty(ref _isEpisodesDrawerOpen, value))
            {
                OnPropertyChanged(nameof(IsEpisodesDrawerOpen));
            }
        }
    }

    private bool _isDrawerSeasonsViewOpen;
    public bool IsDrawerSeasonsViewOpen
    {
        get => _isDrawerSeasonsViewOpen;
        set
        {
            if (SetProperty(ref _isDrawerSeasonsViewOpen, value))
            {
                OnPropertyChanged(nameof(IsDrawerSeasonsViewOpen));
                OnPropertyChanged(nameof(IsDrawerEpisodesViewOpen));
            }
        }
    }

    public bool IsDrawerEpisodesViewOpen => !IsDrawerSeasonsViewOpen;

    private XtreamCategory? _drawerSelectedSeason;
    public XtreamCategory? DrawerSelectedSeason
    {
        get => _drawerSelectedSeason;
        set
        {
            if (SetProperty(ref _drawerSelectedSeason, value))
            {
                OnPropertyChanged(nameof(DrawerSelectedSeason));
                OnPropertyChanged(nameof(DrawerSelectedSeasonName));
            }
        }
    }

    public string DrawerSelectedSeasonName => DrawerSelectedSeason?.Name ?? (_season?.Name ?? "Temporada 1");

    private IReadOnlyList<DrawerSeasonItem> _drawerSeasons = [];
    public IReadOnlyList<DrawerSeasonItem> DrawerSeasons
    {
        get => _drawerSeasons;
        private set => SetProperty(ref _drawerSeasons, value);
    }

    private IReadOnlyList<DrawerEpisodeItem> _drawerEpisodes = [];
    public IReadOnlyList<DrawerEpisodeItem> DrawerEpisodes
    {
        get => _drawerEpisodes;
        private set => SetProperty(ref _drawerEpisodes, value);
    }

    private string _drawerStatusMessage = "";
    public string DrawerStatusMessage
    {
        get => _drawerStatusMessage;
        set
        {
            if (SetProperty(ref _drawerStatusMessage, value))
            {
                OnPropertyChanged(nameof(HasDrawerStatusMessage));
            }
        }
    }

    public bool HasDrawerStatusMessage => !string.IsNullOrWhiteSpace(_drawerStatusMessage);

    public void ToggleEpisodesDrawer()
    {
        if (IsEpisodesDrawerOpen)
        {
            CloseEpisodesDrawer();
        }
        else
        {
            _ = OpenEpisodesDrawerAsync();
        }
    }

    public async Task OpenEpisodesDrawerAsync()
    {
        if (!CanBrowseEpisodes) return;

        // Mutual exclusion with other player overlays
        IsAudioSubtitlesOpen = false;
        IsPlaybackStatsOpen = false;
        CancelNextEpisodeAutoPlay();

        var currentMedia = _mediaWatch?.Item;
        if (currentMedia is not null && !string.IsNullOrWhiteSpace(currentMedia.SeriesId))
        {
            if (_episodes is null || !_episodes.Items.Any(e => e.SeriesId == currentMedia.SeriesId))
            {
                DrawerStatusMessage = "Cargando episodios…";
                await EnsureEpisodesLoadedForSeriesAsync(currentMedia.SeriesId);
                DrawerStatusMessage = "";
            }
        }

        // Determine initial season from currently playing episode
        var currentSeasonNum = currentMedia?.Season ?? 0;
        var matchingSeason = Seasons.FirstOrDefault(s =>
            (currentMedia is not null && s.Id == currentMedia.CategoryId) ||
            (currentSeasonNum > 0 && (s.Id == currentSeasonNum.ToString(CultureInfo.InvariantCulture) || ExtractSeasonNumber(s.Name, s.Id) == currentSeasonNum)));

        DrawerSelectedSeason = matchingSeason ?? Season ?? Seasons.FirstOrDefault();
        IsDrawerSeasonsViewOpen = false;
        RefreshDrawerData();
        IsEpisodesDrawerOpen = true;
    }

    public void CloseEpisodesDrawer()
    {
        IsEpisodesDrawerOpen = false;
        IsDrawerSeasonsViewOpen = false;
        DrawerStatusMessage = "";
    }

    public void SwitchToSeasonsView()
    {
        RefreshDrawerSeasons();
        IsDrawerSeasonsViewOpen = true;
    }

    public void SwitchToEpisodesView()
    {
        RefreshDrawerEpisodes();
        IsDrawerSeasonsViewOpen = false;
    }

    public void SelectDrawerSeason(XtreamCategory season)
    {
        DrawerSelectedSeason = season;
        IsDrawerSeasonsViewOpen = false;
        RefreshDrawerEpisodes();
        RefreshDrawerSeasons();
    }

    public void RefreshDrawerData()
    {
        RefreshDrawerSeasons();
        RefreshDrawerEpisodes();
    }

    public void RefreshDrawerSeasons()
    {
        var rawCategories = Seasons;
        var allEpisodes = _episodes?.Items ?? [];

        DrawerSeasons = rawCategories
            .Select(s =>
            {
                var num = ExtractSeasonNumber(s.Name, s.Id);
                var count = allEpisodes.Count(e =>
                    e.CategoryId == s.Id || (num > 0 && e.Season == num));
                var countLabel = count == 1 ? "1 episodio" : $"{count} episodios";
                var isSelected = DrawerSelectedSeason?.Id == s.Id;
                return new DrawerSeasonItem(s, num, s.Name, count, countLabel, isSelected);
            })
            .OrderBy(s => s.SeasonNumber > 0 ? s.SeasonNumber : int.MaxValue)
            .ThenBy(s => s.Name)
            .ToList();
    }

    public void RefreshDrawerEpisodes()
    {
        var selectedSeason = DrawerSelectedSeason;
        var seasonNum = ExtractSeasonNumber(selectedSeason?.Name, selectedSeason?.Id);
        var allEpisodes = _episodes?.Items ?? [];

        var filtered = allEpisodes
            .Where(e => (selectedSeason is not null && e.CategoryId == selectedSeason.Id) ||
                        (seasonNum > 0 && e.Season == seasonNum) ||
                        selectedSeason is null)
            .OrderBy(e => e.Episode > 0 ? e.Episode : int.MaxValue)
            .ThenBy(e => e.Name)
            .ToList();

        // If no episodes matched by category/seasonNum, fallback to all episodes for this series
        if (filtered.Count == 0 && allEpisodes.Count > 0)
        {
            filtered = allEpisodes.OrderBy(e => e.Episode > 0 ? e.Episode : int.MaxValue).ThenBy(e => e.Name).ToList();
        }

        var result = new List<DrawerEpisodeItem>(filtered.Count);
        for (var i = 0; i < filtered.Count; i++)
        {
            var ep = filtered[i];
            var num = ep.Episode > 0 ? ep.Episode : i + 1;
            var title = string.IsNullOrWhiteSpace(ep.Name) ? $"Episodio {num}" : ep.Name;
            var displayTitle = $"{num}. {title}";
            var isPlaying = _mediaWatch is not null &&
                            (_mediaWatch.Item.StorageKey == ep.StorageKey || _mediaWatch.Item.Id == ep.Id);
            var playStatusText = isPlaying ? "Reproduciendo" : "";

            // Check progress from _progress store
            var progress = _progress.FirstOrDefault(p => p.Item.StorageKey == ep.StorageKey || p.Item.Id == ep.Id);
            double progressFraction = 0.0;
            bool hasProgress = false;
            string durationText = "";

            if (progress is not null && progress.DurationMilliseconds > 0)
            {
                progressFraction = Math.Clamp((double)progress.PositionMilliseconds / progress.DurationMilliseconds, 0.0, 1.0);
                hasProgress = progressFraction > 0.01 && !progress.Completed;
                var minutes = (int)Math.Round(progress.DurationMilliseconds / 60000.0);
                if (minutes > 0) durationText = $"{minutes} min";
            }

            result.Add(new DrawerEpisodeItem(
                ep,
                num,
                title,
                displayTitle,
                ep.PosterUrl,
                ep.Description,
                durationText,
                progressFraction,
                hasProgress,
                isPlaying,
                playStatusText));
        }

        DrawerEpisodes = result;
    }

    public async Task PlayEpisodeFromDrawerAsync(MediaItem episode)
    {
        if (_disposed) return;

        // If clicking the episode that is already playing, do NOT restart/reload video!
        if (_mediaWatch is not null &&
            (_mediaWatch.Item.StorageKey == episode.StorageKey || _mediaWatch.Item.Id == episode.Id))
        {
            return;
        }

        CancelNextEpisodeAutoPlay();
        SelectedEpisode = episode;

        var targetSeason = Seasons.FirstOrDefault(s => s.Id == episode.CategoryId || ExtractSeasonNumber(s.Name, s.Id) == episode.Season);
        if (targetSeason is not null && _season?.Id != targetSeason.Id)
        {
            _season = targetSeason;
            OnPropertyChanged(nameof(Season));
            OnPropertyChanged(nameof(Episodes));
        }

        try
        {
            await PlayMediaItemAsync(episode);
            CloseEpisodesDrawer();
        }
        catch (Exception error)
        {
            _logger.LogError("drawer_play_episode", error);
            DrawerStatusMessage = "No se pudo reproducir el episodio seleccionado.";
        }
    }

    public static int ExtractSeasonNumber(string? name, string? id = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var match = Regex.Match(name, @"\d+");
            if (match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        if (!string.IsNullOrWhiteSpace(id) && int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idParsed))
        {
            return idParsed;
        }
        return 1;
    }
}
