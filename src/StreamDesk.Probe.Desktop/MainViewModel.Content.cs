using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly IContentStore? _contentStore;
    private MediaCatalog? _movies, _series, _episodes;
    private MediaItem? _selectedMedia, _selectedEpisode, _selectedDetails;
    private XtreamCategory? _mediaCategory, _season;
    private string _mediaSearch = "", _mediaCategorySearch = "", _contentStatus = "Explora películas y series de tu cuenta.", _epgStatus = "Elige un canal para ver su programación.";
    private bool _contentBusy;
    private CancellationTokenSource? _mediaFilter, _detailRequest, _epgRequest;
    private Task _progressWrites = Task.CompletedTask, _detailTask = Task.CompletedTask, _autoAdvanceTask = Task.CompletedTask;
    private readonly SemaphoreSlim _mediaCapture = new(1, 1);
    private MediaWatch? _mediaWatch;
    private IReadOnlyList<MediaProgress> _progress = [];
    private MediaPlaybackDetails _mediaDetails = MediaPlaybackDetails.Empty;
    public bool ShowLiveCatalog => CurrentTabIndex is >= 1 and <= 3;
    public bool ShowMediaCatalog => CurrentTabIndex is 5 or 6;
    public bool ShowMovieBrowse => CurrentTabIndex == 5 && SelectedMedia is null;
    public bool ShowMovieDetails => CurrentTabIndex == 5 && SelectedMedia?.Kind == ContentKind.Movie;
    public bool ShowSeriesBrowse => CurrentTabIndex == 6 && SelectedMedia is null;
    public bool ShowSeriesDetails => CurrentTabIndex == 6 && SelectedMedia?.Kind == ContentKind.Series;
    private bool _isImmersiveMode;
    private bool _autoRandomNextEpisode;
    private readonly HashSet<string> _randomCyclePlayedKeys = new(StringComparer.Ordinal);
    public Func<int, int> RandomIndex { get; set; } = maxExclusive => Random.Shared.Next(maxExclusive);
    public bool IsImmersiveMode
    {
        get => _isImmersiveMode && ShowTv;
        set
        {
            if (SetProperty(ref _isImmersiveMode, value))
            {
                OnPropertyChanged(nameof(IsEpisodePlayerFocus));
                OnPropertyChanged(nameof(ImmersiveReturnLabel));
                OnPropertyChanged(nameof(ShowPlayerPane));
                OnPropertyChanged(nameof(ShowSeriesDetails));
                OnPropertyChanged(nameof(ShowMovieDetails));
            }
        }
    }
    public bool IsEpisodePlayerFocus
    {
        get => IsImmersiveMode && ShowSeriesDetails;
        set => IsImmersiveMode = value;
    }
    public string ImmersiveReturnLabel => (ShowSeriesDetails || _mediaWatch?.Item.Kind == ContentKind.Episode)
        ? "Volver a episodios"
        : (ShowMovieDetails || CurrentTabIndex == 5 || _mediaWatch?.Item.Kind == ContentKind.Movie)
            ? "Volver a películas"
            : "Volver a canales";
    public void ReturnFromImmersive() => IsImmersiveMode = false;
    public void ReturnToEpisodes() => ReturnFromImmersive();
    public bool AutoRandomNextEpisode
    {
        get => _autoRandomNextEpisode;
        set => SetProperty(ref _autoRandomNextEpisode, value);
    }
    public bool CanPlayRandomEpisode => ShowSeriesDetails && !ContentBusy && !IsBusy && !IsCatalogLoading && GetPlayableEpisodes().Count > 0;
    public static bool IsPlayableEpisode(MediaItem? item) =>
        item is not null &&
        item.Kind == ContentKind.Episode &&
        !string.IsNullOrWhiteSpace(item.Id) &&
        !string.IsNullOrWhiteSpace(item.Name) &&
        MediaMetadata.SafeExtension(item.Extension);
    private IReadOnlyList<MediaItem> GetPlayableEpisodes() => (_episodes?.Items ?? []).Where(IsPlayableEpisode).ToArray();
    public bool ShowMediaBrowse => ShowMovieBrowse || ShowSeriesBrowse;
    public bool ShowPlayerPane => !ShowMediaBrowse || IsImmersiveMode;
    public bool ShowContinueWatching => CurrentTabIndex == 7;
    public string VersionLabel => typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "Candidato";
    public string HeaderSummary => ShowLiveCatalog ? CatalogSummary : ShowContinueWatching ? $"{ContinueWatching.Count} en curso" : $"{MediaItems.Count} títulos";
    public bool HasMovies => _movies?.Items.Count > 0;
    public bool HasSeries => _series?.Items.Count > 0;
    public bool HasNoMediaItems => ShowMediaBrowse && MediaItems.Count == 0 && !ContentBusy;
    public bool ContentBusy { get => _contentBusy; private set { if (SetProperty(ref _contentBusy, value)) { OnPropertyChanged(nameof(IsWorking)); OnPropertyChanged(nameof(HasNoMediaItems)); OnPropertyChanged(nameof(CanPlayRandomEpisode)); } } }
    public string ContentStatus
    {
        get => _contentStatus;
        private set { if (SetProperty(ref _contentStatus, value)) OnPropertyChanged(nameof(FooterStatus)); }
    }
    public string EpgStatus { get => _epgStatus; private set => SetProperty(ref _epgStatus, value); }
    public string EpgNow { get; private set; } = "Sin programación actual.";
    public string EpgNext { get; private set; } = "Sin programación siguiente.";
    public IReadOnlyList<MediaItem> MediaItems { get; private set; } = [];
    public string MediaDescription => (_selectedDetails ?? SelectedMedia)?.Description ?? "Elige un título para ver su sinopsis.";
    public string MediaPosterUrl => (_selectedDetails ?? SelectedMedia)?.PosterUrl ?? "";
    public string MediaCategoryLabel => SelectedMedia is null ? "" : MediaCategories.FirstOrDefault(category => category.Id == SelectedMedia.CategoryId)?.Name ?? "";
    public string SeriesSummary => $"{Seasons.Count} temporadas · {_episodes?.Items.Count ?? 0} episodios";
    public IReadOnlyList<XtreamCategory> MediaCategories
    {
        get
        {
            var rawCategories = (CurrentTabIndex == 6 ? _series : _movies)?.Categories ?? [];
            var allowed = _parentalControl?.IsLocked == true
                ? rawCategories.Where(c => !_parentalControl.IsRestrictedCategory(c.Name))
                : rawCategories;
            return new[] { new XtreamCategory("", "Todas las categorías") }.Concat(allowed).ToArray();
        }
    }
    public IReadOnlyList<XtreamCategory> VisibleMediaCategories
    {
        get
        {
            var categories = MediaCategories;
            var query = CatalogSearch.Normalize(MediaCategorySearch);
            if (query.Length == 0 || categories.Count == 0) return categories;
            return new[] { categories[0] }.Concat(categories.Skip(1).Where(category =>
                category.Id == MediaCategory?.Id || CatalogSearch.Normalize(category.Name).Contains(query, StringComparison.Ordinal))).ToArray();
        }
    }
    public IReadOnlyList<XtreamCategory> Seasons => _episodes?.Categories ?? [];
    public IReadOnlyList<MediaItem> Episodes => (_episodes?.Items ?? []).Where(item => Season is null || item.CategoryId == Season.Id).ToArray();
    public IReadOnlyList<MediaProgress> ContinueWatching => _progress.Where(item => !item.Completed && item.PositionMilliseconds > 0).ToArray();
    public MediaProgress? SelectedProgress { get; set; }
    public MediaItem? SelectedMedia
    {
        get => _selectedMedia;
        set
        {
            if (!SetProperty(ref _selectedMedia, value)) return;
            _selectedDetails = null; _isImmersiveMode = false; _randomCyclePlayedKeys.Clear();
            if (value is null && ShowMediaCatalog) ContentStatus = $"Películas: {_movies?.Items.Count ?? 0} · Series: {_series?.Items.Count ?? 0}";
            foreach (var name in new[] { nameof(MediaDescription), nameof(MediaPosterUrl), nameof(MediaCategoryLabel), nameof(SeriesSummary), nameof(ShowMovieBrowse), nameof(ShowMovieDetails), nameof(ShowSeriesBrowse), nameof(ShowSeriesDetails), nameof(ShowMediaBrowse), nameof(ShowPlayerPane), nameof(NowPlaying), nameof(IsEpisodePlayerFocus), nameof(IsImmersiveMode), nameof(ImmersiveReturnLabel), nameof(CanPlayRandomEpisode) }) OnPropertyChanged(name);
            _detailTask = LoadMediaDetailsAsync(value);
        }
    }
    public MediaItem? SelectedEpisode
    {
        get => _selectedEpisode;
        set { if (SetProperty(ref _selectedEpisode, value)) OnPropertyChanged(nameof(NowPlaying)); OnPropertyChanged(nameof(IsEpisodePlayerFocus)); }
    }
    public XtreamCategory? MediaCategory { get => _mediaCategory ?? MediaCategories.FirstOrDefault(); set { if (SetProperty(ref _mediaCategory, value)) { OnPropertyChanged(nameof(VisibleMediaCategories)); _ = FilterMediaAsync(); } } }
    public XtreamCategory? Season
    {
        get => _season;
        set
        {
            if (SetProperty(ref _season, value))
            {
                OnPropertyChanged(nameof(Episodes));
                SelectedEpisode = Episodes.FirstOrDefault();
            }
        }
    }
    public string MediaSearch
    {
        get => _mediaSearch;
        set
        {
            if (SetProperty(ref _mediaSearch, value))
            {
                OnPropertyChanged(nameof(HasMediaSearch));
                _ = FilterMediaAsync(true);
            }
        }
    }
    public bool HasMediaSearch => !string.IsNullOrEmpty(MediaSearch);
    public string MediaCategorySearch
    {
        get => _mediaCategorySearch;
        set
        {
            if (!SetProperty(ref _mediaCategorySearch, value)) return;
            OnPropertyChanged(nameof(HasMediaCategorySearch));
            OnPropertyChanged(nameof(VisibleMediaCategories));
        }
    }
    public bool HasMediaCategorySearch => !string.IsNullOrWhiteSpace(MediaCategorySearch);
    public IReadOnlyList<MediaTrack> AudioTracks => _mediaDetails.AudioTracks;
    public IReadOnlyList<MediaTrack> SubtitleTracks => _mediaDetails.SubtitleTracks;
    public MediaTrack? SelectedAudioTrack { get; set; }
    public MediaTrack? SelectedSubtitleTrack { get; set; }
    public bool CanSeek => _mediaWatch is not null && _mediaDetails.CanSeek && _mediaDetails.LengthMilliseconds > 0;
    public double DurationSeconds => _mediaDetails.LengthMilliseconds / 1000d;
    public double PositionSeconds => _mediaDetails.TimeMilliseconds / 1000d;
    public string PositionLabel => $"{TimeSpan.FromMilliseconds(_mediaDetails.TimeMilliseconds):hh\\:mm\\:ss} / {TimeSpan.FromMilliseconds(_mediaDetails.LengthMilliseconds):hh\\:mm\\:ss}";

    public async Task ProbeContentAsync()
    {
        if (_disposed || ContentBusy || !CanConnect) return;
        var version = _sessionVersion;
        ContentBusy = true;
        CancellationTokenSource? operation = null;
        var failed = false;
        try
        {
            if (_client is null) await ConnectProfileAsync();
            if (version != _sessionVersion || _disposed) return;
            if (_client is not IContentProvider provider) { ContentStatus = "Tu cuenta no incluye películas ni series."; return; }
            operation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
            _operation = operation;
            foreach (var kind in new[] { ContentKind.Movie, ContentKind.Series })
            {
                try
                {
                    ContentStatus = kind == ContentKind.Movie ? "Cargando películas…" : "Cargando series…";
                    var catalog = await provider.GetMediaCatalogAsync(kind, operation.Token);
                    operation.Token.ThrowIfCancellationRequested(); if (version != _sessionVersion) return;
                    if (_connectedProfileId is { } id && _contentStore is not null) await _contentStore.SaveCatalogAsync(id, kind, "", catalog, operation.Token);
                    if (version != _sessionVersion) return;
                    if (kind == ContentKind.Movie) _movies = catalog; else _series = catalog;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { failed = true; _logger.LogError("content_catalog", error); }
            }
            if (version != _sessionVersion || _disposed) return;
            ContentStatus = $"Películas: {_movies?.Items.Count ?? 0} · Series: {_series?.Items.Count ?? 0}" + (failed ? " · Se conserva la lista guardada." : "");
            NotifyContent(); await FilterMediaAsync();
        }
        catch (OperationCanceledException) { if (version == _sessionVersion) ContentStatus = "Carga cancelada."; }
        catch (Exception error) { _logger.LogError("content_probe", error); if (version == _sessionVersion) ContentStatus = "No se pudo actualizar el catálogo."; }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; operation?.Dispose(); ContentBusy = false; }
    }

    private async Task LoadContentCacheAsync(string? profile, int version, CancellationToken token)
    {
        if (profile is null || _contentStore is null) return;
        var movies = await _contentStore.LoadCatalogAsync(profile, ContentKind.Movie, cancellationToken: token);
        var series = await _contentStore.LoadCatalogAsync(profile, ContentKind.Series, cancellationToken: token);
        var progress = await _contentStore.LoadProgressAsync(profile, token);
        if (version != _sessionVersion || _disposed) return;
        ContentStatus = $"Películas: {movies?.Items.Count ?? 0} · Series: {series?.Items.Count ?? 0}";
        _movies = movies; _series = series; _progress = progress; NotifyContent(); await FilterMediaAsync();
    }

    private async Task FilterMediaAsync(bool debounce = false)
    {
        if (_disposed) return;
        _mediaFilter?.Cancel(); using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _mediaFilter = request;
        var catalog = CurrentTabIndex == 6 ? _series : _movies; var query = CatalogSearch.Normalize(MediaSearch); var category = MediaCategory?.Id;
        var restrictedCatIds = _parentalControl?.IsLocked == true
            ? (catalog?.Categories ?? []).Where(c => _parentalControl.IsRestrictedCategory(c.Name)).Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        try
        {
            if (debounce) await Task.Delay(180, request.Token);
            var matches = await Task.Run(() => (catalog?.Items ?? []).Where(item =>
            {
                request.Token.ThrowIfCancellationRequested();
                if (restrictedCatIds is not null && restrictedCatIds.Contains(item.CategoryId ?? "")) return false;
                return (string.IsNullOrEmpty(category) || item.CategoryId == category) && CatalogSearch.Normalize(item.Name).Contains(query, StringComparison.Ordinal);
            }).ToArray(), request.Token);
            if (!ReferenceEquals(_mediaFilter, request) || _disposed) return;
            MediaItems = matches; OnPropertyChanged(nameof(MediaItems)); OnPropertyChanged(nameof(HasNoMediaItems)); OnPropertyChanged(nameof(HeaderSummary));
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_mediaFilter, request)) _mediaFilter = null; }
    }

    private async Task LoadMediaDetailsAsync(MediaItem? item)
    {
        _detailRequest?.Cancel(); _episodes = null; _randomCyclePlayedKeys.Clear(); Season = null; SelectedEpisode = null; OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes)); OnPropertyChanged(nameof(CanPlayRandomEpisode));
        if (item is null || _disposed) return;
        ContentStatus = item.Kind == ContentKind.Series ? "Cargando temporadas y episodios…" : "Cargando información de la película…";
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _detailRequest = request;
        var version = _sessionVersion;
        try
        {
            if (_client is null) await ConnectProfileAsync();
            request.Token.ThrowIfCancellationRequested();
            if (_client is not IContentProvider provider || version != _sessionVersion) return;
            if (item.Kind == ContentKind.Movie)
            {
                var details = await provider.GetMovieInfoAsync(item, request.Token);
                if (!ReferenceEquals(_selectedMedia, item) || version != _sessionVersion) return;
                _selectedDetails = details; OnPropertyChanged(nameof(MediaDescription)); OnPropertyChanged(nameof(MediaPosterUrl));
            }
            else
            {
                var cached = _contentStore is not null && _connectedProfileId is { } profile ? await _contentStore.LoadCatalogAsync(profile, ContentKind.Episode, item.Id, request.Token) : null;
                var rawEpisodes = cached ?? await provider.GetEpisodesAsync(item.Id, request.Token);
                var episodes = rawEpisodes with { Items = rawEpisodes.Items.Where(IsPlayableEpisode).ToArray() };
                request.Token.ThrowIfCancellationRequested(); if (!ReferenceEquals(_selectedMedia, item) || version != _sessionVersion) return;
                if (cached is null && _contentStore is not null && _connectedProfileId is { } id) await _contentStore.SaveCatalogAsync(id, ContentKind.Episode, item.Id, episodes, request.Token);
                if (version != _sessionVersion) return;
                _episodes = episodes; _randomCyclePlayedKeys.Clear(); Season = Seasons.FirstOrDefault(); OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes)); OnPropertyChanged(nameof(SeriesSummary)); OnPropertyChanged(nameof(CanPlayRandomEpisode));
                SelectedEpisode ??= Episodes.FirstOrDefault();
                ContentStatus = episodes.Items.Count == 0 ? "Sin episodios disponibles." : $"{episodes.Items.Count} episodios disponibles.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_details", error); if (version == _sessionVersion) ContentStatus = "No se pudo cargar la ficha."; }
        finally { if (ReferenceEquals(_detailRequest, request)) _detailRequest = null; }
    }

    public Task PlayMediaAsync(bool restart = false) => PlayMediaItemAsync(ShowContinueWatching ? SelectedProgress?.Item : SelectedMedia?.Kind == ContentKind.Series ? SelectedEpisode : _selectedDetails ?? SelectedMedia, restart);
    private async Task PlayMediaItemAsync(MediaItem? item, bool restart = false)
    {
        if (item is null || _disposed || IsBusy || IsCatalogLoading) { if (item is null) ContentStatus = "Elige una película o episodio."; return; }
        var version = _sessionVersion;
        try
        {
            await CaptureMediaAsync(true);
            if (_client is null) await ConnectProfileAsync();
            if (_disposed || version != _sessionVersion || _client is not IContentProvider provider || _account is null) return;
            if (_account.ExpiresAt <= DateTimeOffset.UtcNow) throw new PlaybackException(PlaybackFailure.Expired);
            if (!_playbackService.HasActiveMedia && _account.HasReachedConnectionLimit) throw new PlaybackException(PlaybackFailure.ConnectionLimit);
            if (!MediaMetadata.SafeExtension(item.Extension) && item.Kind == ContentKind.Movie) item = await provider.GetMovieInfoAsync(item, _session.Token);
            var uri = provider.CreateMediaUri(item);
            if (version != _sessionVersion || _disposed) return;
            var resume = _progress.FirstOrDefault(entry => entry.Item.Kind == item.Kind && entry.Item.StorageKey == item.StorageKey);
            var position = restart || resume?.Completed != false ? 0 : resume.PositionMilliseconds;
            _watch = null; _playingChannelId = null;
            OnPropertyChanged(nameof(PlayingChannelId));
            _mediaDetails = MediaPlaybackDetails.Empty; NotifyMediaDetails();
            _mediaWatch = new(item, _connectedProfileId, version, _playbackService.Snapshot.Sequence) { Position = position, Length = resume?.DurationMilliseconds ?? 0 };
            _playbackService.Options = _playbackService.Options with { OnDemand = true, StartPositionMilliseconds = position };
            if (item.Kind == ContentKind.Episode) _randomCyclePlayedKeys.Add(item.StorageKey);
            IsImmersiveMode = true;
            OnPropertyChanged(nameof(NowPlaying)); OnPropertyChanged(nameof(IsEpisodePlayerFocus)); OnPropertyChanged(nameof(IsImmersiveMode)); OnPropertyChanged(nameof(ImmersiveReturnLabel));
            await _playbackService.PlayAsync(uri, _session.Token);
            OnPropertyChanged(nameof(PlayerHandle));
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_play", error); ContentStatus = error is PlaybackException playback ? playback.Message : "No se pudo reproducir este título."; }
    }

    public async Task NavigateEpisodeAsync(int direction)
    {
        var items = _episodes?.Items ?? []; var key = _mediaWatch?.Item.StorageKey ?? SelectedEpisode?.StorageKey;
        var index = items.ToList().FindIndex(item => item.StorageKey == key); var next = index + direction;
        if (direction is not (-1 or 1) || index < 0 || next < 0 || next >= items.Count) return;
        SelectedEpisode = items[next]; await PlayMediaItemAsync(items[next]);
    }

    public async Task PlayRandomEpisodeAsync()
    {
        if (_disposed || IsBusy || IsCatalogLoading || ContentBusy) return;
        var chosen = SelectNextRandomEpisode();
        if (chosen is null)
        {
            ContentStatus = "No hay episodios reproducibles en esta serie.";
            return;
        }
        var targetSeason = Seasons.FirstOrDefault(season => season.Id == chosen.CategoryId);
        if (targetSeason is not null && _season?.Id != targetSeason.Id)
        {
            _season = targetSeason;
            OnPropertyChanged(nameof(Season));
            OnPropertyChanged(nameof(Episodes));
        }
        SelectedEpisode = chosen;
        TriggerOsd($"Al azar: {chosen.Name}");
        await PlayMediaItemAsync(chosen);
    }

    private async Task AdvanceRandomEpisodeOnEndedAsync(MediaWatch completedMedia)
    {
        if (_disposed || !AutoRandomNextEpisode || completedMedia.Version != _sessionVersion || !ReferenceEquals(completedMedia, _mediaWatch)) return;
        await PlayRandomEpisodeAsync();
    }

    private MediaItem? SelectNextRandomEpisode()
    {
        var playable = GetPlayableEpisodes();
        if (playable.Count == 0) return null;
        if (playable.Count == 1)
        {
            _randomCyclePlayedKeys.Clear();
            _randomCyclePlayedKeys.Add(playable[0].StorageKey);
            return playable[0];
        }
        var currentKey = _mediaWatch?.Item.Kind == ContentKind.Episode ? _mediaWatch.Item.StorageKey : SelectedEpisode?.StorageKey;
        if (!string.IsNullOrEmpty(currentKey) && playable.Any(item => item.StorageKey == currentKey))
            _randomCyclePlayedKeys.Add(currentKey);
        var candidates = playable.Where(item => item.StorageKey != currentKey && !_randomCyclePlayedKeys.Contains(item.StorageKey)).ToArray();
        if (candidates.Length == 0)
        {
            _randomCyclePlayedKeys.Clear();
            if (!string.IsNullOrEmpty(currentKey) && playable.Any(item => item.StorageKey == currentKey))
                _randomCyclePlayedKeys.Add(currentKey);
            candidates = playable.Where(item => item.StorageKey != currentKey).ToArray();
            if (candidates.Length == 0) candidates = playable.ToArray();
        }
        var rawIndex = RandomIndex(candidates.Length);
        var index = ((rawIndex % candidates.Length) + candidates.Length) % candidates.Length;
        var chosen = candidates[index];
        _randomCyclePlayedKeys.Add(chosen.StorageKey);
        return chosen;
    }

    private void EndNavigateEpisodePlaceholder()
    {
    }

    public async Task CaptureMediaAsync(bool persist = false)
    {
        var watch = _mediaWatch;
        if (watch is null) return;
        await _mediaCapture.WaitAsync();
        try
        {
            if (!ReferenceEquals(watch, _mediaWatch)) return;
            var details = await _playbackService.ReadDetailsAsync();
            if (!ReferenceEquals(watch, _mediaWatch)) return;
            if (details.LengthMilliseconds > 0) { watch.Position = details.TimeMilliseconds; watch.Length = details.LengthMilliseconds; }
            if (!_disposed) { _mediaDetails = details; NotifyMediaDetails(); }
            if (watch.Played && (persist || DateTimeOffset.UtcNow - watch.LastSaved > TimeSpan.FromSeconds(15)))
            { watch.LastSaved = DateTimeOffset.UtcNow; _progressWrites = SaveMediaProgressAsync(_progressWrites, watch); await _progressWrites; }
        }
        catch (Exception error) { _logger.LogError("content_position", error); }
        finally { _mediaCapture.Release(); }
    }
    public async Task RefreshPlaybackDetailsAsync()
    {
        UpdateEpgLabels();
        if (_disposed || !_playbackService.HasActiveMedia) return;
        if (_mediaWatch is not null) { await CaptureMediaAsync(); return; }
        var version = _sessionVersion; var channel = _playingChannelId;
        try
        {
            var details = await _playbackService.ReadDetailsAsync(_session.Token);
            if (!_disposed && version == _sessionVersion && channel == _playingChannelId && _mediaWatch is null) { _mediaDetails = details; NotifyMediaDetails(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("playback_details", error); }
    }
    private async Task SaveMediaProgressAsync(Task previous, MediaWatch watch)
    {
        var progress = new MediaProgress(watch.Item, watch.Completed && watch.Length > 0 ? watch.Length : watch.Position, watch.Length, DateTimeOffset.UtcNow, watch.Completed);
        await previous;
        if (_contentStore is null || watch.ProfileId is null || !watch.Played) return;
        try
        {
            await _contentStore.SaveProgressAsync(watch.ProfileId, progress);
            if (_disposed || watch.Version != _sessionVersion) return;
            _progress = await _contentStore.LoadProgressAsync(watch.ProfileId);
            if (!_disposed && watch.Version == _sessionVersion) OnPropertyChanged(nameof(ContinueWatching));
        }
        catch (Exception error) { _logger.LogError("content_progress", error); }
    }
    public async Task SeekToAsync(double seconds)
    {
        try
        {
            var clamped = Math.Clamp(seconds, 0, DurationSeconds);
            if (!CanSeek || !await _playbackService.SeekAsync((long)(clamped * 1000), _session.Token)) ContentStatus = "Este vídeo no permite adelantar ni retroceder.";
            else
            {
                _mediaDetails = _mediaDetails with { TimeMilliseconds = (long)(clamped * 1000) };
                NotifyMediaDetails();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_seek", error); ContentStatus = "No se pudo cambiar la posición."; }
    }
    public async Task SeekRelativeAsync(double deltaSeconds)
    {
        if (!CanSeek) return;
        var target = Math.Clamp(PositionSeconds + deltaSeconds, 0, DurationSeconds);
        await SeekToAsync(target);
        TriggerOsd(deltaSeconds < 0 ? $"⏪ {deltaSeconds:0}s" : $"⏩ +{deltaSeconds:0}s");
    }
    public async Task SelectTrackAsync(bool subtitles)
    {
        var track = subtitles ? SelectedSubtitleTrack : SelectedAudioTrack;
        try { if (track is null || !await _playbackService.SelectTrackAsync(subtitles, track.Id, _session.Token)) ContentStatus = " esa pista no está disponible."; }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("media_track", error); ContentStatus = "No se pudo cambiar el idioma o subtítulo."; }
    }
    private void NotifyMediaDetails()
    {
        SelectedAudioTrack = AudioTracks.FirstOrDefault(item => item.Id == _mediaDetails.AudioTrack);
        SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(item => item.Id == _mediaDetails.SubtitleTrack);
        foreach (var name in new[] { nameof(AudioTracks), nameof(SubtitleTracks), nameof(SelectedAudioTrack), nameof(SelectedSubtitleTrack), nameof(CanSeek), nameof(DurationSeconds), nameof(PositionSeconds), nameof(PositionLabel) }) OnPropertyChanged(name);
    }
    private void NotifyContent()
    {
        SelectedProgress ??= ContinueWatching.FirstOrDefault();
        foreach (var name in new[] { nameof(HasMovies), nameof(HasSeries), nameof(HasNoMediaItems), nameof(MediaCategories), nameof(VisibleMediaCategories), nameof(MediaCategoryLabel), nameof(ContinueWatching), nameof(SelectedProgress) }) OnPropertyChanged(name);
    }
    private void ResetContent()
    {
        _detailRequest?.Cancel(); _mediaFilter?.Cancel(); _epgRequest?.Cancel();
        _movies = _series = _episodes = null; _progress = []; _selectedMedia = _selectedEpisode = _selectedDetails = null; _season = _mediaCategory = null; _isImmersiveMode = false; _randomCyclePlayedKeys.Clear();
        MediaItems = []; _mediaDetails = MediaPlaybackDetails.Empty;
        ContentStatus = "Explora películas y series de tu cuenta."; EpgStatus = "Elige un canal para ver su programación.";
        NotifyContent(); NotifyMediaDetails(); OnPropertyChanged(nameof(MediaItems)); OnPropertyChanged(nameof(HasNoMediaItems)); OnPropertyChanged(nameof(SelectedMedia)); OnPropertyChanged(nameof(MediaDescription)); OnPropertyChanged(nameof(MediaPosterUrl)); OnPropertyChanged(nameof(MediaCategoryLabel)); OnPropertyChanged(nameof(SeriesSummary)); OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes));
        OnPropertyChanged(nameof(ShowMovieBrowse)); OnPropertyChanged(nameof(ShowMovieDetails)); OnPropertyChanged(nameof(ShowSeriesBrowse)); OnPropertyChanged(nameof(ShowSeriesDetails)); OnPropertyChanged(nameof(ShowMediaBrowse)); OnPropertyChanged(nameof(ShowPlayerPane)); OnPropertyChanged(nameof(NowPlaying)); OnPropertyChanged(nameof(IsEpisodePlayerFocus)); OnPropertyChanged(nameof(IsImmersiveMode)); OnPropertyChanged(nameof(ImmersiveReturnLabel)); OnPropertyChanged(nameof(CanPlayRandomEpisode));
        _epgPrograms = []; UpdateEpgLabels();
    }
    private sealed class MediaWatch(MediaItem item, string? profileId, int version, long afterSequence)
    {
        public MediaItem Item { get; } = item; public string? ProfileId { get; } = profileId; public int Version { get; } = version;
        public long AfterSequence { get; } = afterSequence;
        public bool Played, Completed, RandomAdvanceTriggered; public long Position, Length; public DateTimeOffset LastSaved;
    }
}
