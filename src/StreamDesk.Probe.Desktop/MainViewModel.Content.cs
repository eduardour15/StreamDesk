using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed record MediaSortOption(string Label, MainViewModel.MediaSortOrder Value);

public sealed partial class MainViewModel
{
    private const int MediaGridPageSize = 48;
    private const int HomeCarouselLimit = 12;
    private readonly IContentStore? _contentStore;
    private MediaCatalog? _movies, _series, _episodes;
    private MediaCatalogSearch? _movieIndex, _seriesIndex;
    private bool _isMediaFiltering;
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
    public bool ShowLiveCatalog => CurrentTabIndex is 1 or 3;
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
    public bool ShowPlayerPane => (!ShowMediaBrowse || IsImmersiveMode) && (!ShowContinueWatching || _playback.IsActive || _mediaWatch is not null);
    public bool ShowContinueWatching => CurrentTabIndex == 7;
    public bool HasContinueWatching => ContinueWatching.Count > 0;
    public IReadOnlyList<MediaProgress> HomeContinueWatching => ContinueWatching.Take(8).ToArray();
    public IReadOnlyList<MediaItem> HomeMovies => (_movies?.Items ?? [])
        .Where(item => item.Kind == ContentKind.Movie).DistinctBy(item => item.Id).Take(HomeCarouselLimit).ToArray();
    public IReadOnlyList<MediaItem> HomeSeries => (_series?.Items ?? [])
        .Where(item => item.Kind == ContentKind.Series).DistinctBy(item => item.Id).Take(HomeCarouselLimit).ToArray();
    public bool HasHomeMovies => HomeMovies.Count > 0;
    public bool HasHomeSeries => HomeSeries.Count > 0;
    public bool HomeCatalogSkeletonVisible => IsCatalogSyncing && !HasHomeMovies && !HasHomeSeries;
    public MediaItem? HomeFeaturedMedia => ContinueWatching.FirstOrDefault()?.Item
        ?? _movies?.Items.FirstOrDefault(item => item.Kind == ContentKind.Movie && !string.IsNullOrWhiteSpace(item.PosterUrl))
        ?? _series?.Items.FirstOrDefault(item => item.Kind == ContentKind.Series && !string.IsNullOrWhiteSpace(item.PosterUrl))
        ?? _movies?.Items.FirstOrDefault(item => item.Kind == ContentKind.Movie)
        ?? _series?.Items.FirstOrDefault(item => item.Kind == ContentKind.Series);
    public MediaProgress? HomeFeaturedProgress => HomeFeaturedMedia is { } item
        ? ContinueWatching.FirstOrDefault(progress => progress.Item.Kind == item.Kind && progress.Item.StorageKey == item.StorageKey)
        : null;
    public XtreamChannel? HomeFeaturedChannel => HomeFeaturedMedia is null ? LastChannel : null;
    public bool HasHomeFeatured => HomeFeaturedMedia is not null || HomeFeaturedChannel is not null;
    public bool HasHomeFeaturedDescription => !string.IsNullOrWhiteSpace(HomeFeaturedDescription);
    public bool HasHomeFeaturedArt => !string.IsNullOrWhiteSpace(HomeFeaturedPosterUrl);
    public string HomeFeaturedTitle => HomeFeaturedMedia?.Name ?? HomeFeaturedChannel?.Name
        ?? (ActiveProfile is null ? "Añade una cuenta para empezar" : "Tu catálogo aparecerá aquí");
    public string HomeFeaturedDescription => HomeFeaturedMedia?.Plot is { Length: > 0 } plot
        ? plot[..Math.Min(plot.Length, 420)]
        : string.Empty;
    public string HomeFeaturedPosterUrl => HomeFeaturedMedia?.PosterUrl ?? HomeFeaturedChannel?.LogoUrl ?? string.Empty;
    public string HomeFeaturedTypeLabel => HomeFeaturedProgress is { Item.Kind: ContentKind.Episode, Item.Season: > 0, Item.Episode: > 0 } progress
        ? $"CONTINUAR · T{progress.Item.Season:00} E{progress.Item.Episode:00}"
        : HomeFeaturedProgress is not null ? "CONTINUAR VIENDO"
        : HomeFeaturedMedia?.Kind switch
        {
            ContentKind.Movie => "PELÍCULA",
            ContentKind.Series => "SERIE",
            ContentKind.Episode => "EPISODIO",
            _ when HomeFeaturedChannel is not null => "TV EN VIVO",
            _ => ""
        };
    public string HomeFeaturedActionLabel => HomeFeaturedProgress is not null ? "Continuar"
        : HomeFeaturedMedia?.Kind switch
        {
            ContentKind.Movie => "Reproducir",
            ContentKind.Series => "Ver episodios",
            ContentKind.Episode => "Continuar",
            _ when HomeFeaturedChannel is not null => "Ver canal",
            _ => ActiveProfile is null ? "Configurar cuenta" : "Abrir TV en vivo"
        };
    public bool CanShowHomeFeaturedDetails => HomeFeaturedMedia?.Kind is ContentKind.Movie or ContentKind.Series;
    public string HomeEmptyDescription => ActiveProfile is null
        ? "Configura tu proveedor y aquí aparecerán los canales, películas y series disponibles."
        : "Cuando el catálogo esté disponible, podrás retomarlo o explorar sus películas y series.";

    private bool _isMediaGridView = true;
    private bool _mediaFavoritesOnly;
    public bool IsMediaGridView
    {
        get => _isMediaGridView;
        set
        {
            if (!SetProperty(ref _isMediaGridView, value)) return;
            OnPropertyChanged(nameof(IsMediaListView));
            OnPropertyChanged(nameof(VisibleMediaItems));
        }
    }
    public bool IsMediaListView
    {
        get => !IsMediaGridView;
        set { if (value) IsMediaGridView = false; }
    }
    public bool MediaFavoritesOnly
    {
        get => _mediaFavoritesOnly;
        set { if (SetProperty(ref _mediaFavoritesOnly, value)) _ = FilterMediaAsync(); }
    }

    public enum MediaSortOrder { Default, TitleAsc, TitleDesc }
    public static IReadOnlyList<MediaSortOption> MediaSortOptions { get; } =
    [
        new("Orden del catálogo", MediaSortOrder.Default),
        new("Título A–Z", MediaSortOrder.TitleAsc),
        new("Título Z–A", MediaSortOrder.TitleDesc)
    ];
    private MediaSortOrder _selectedSortOrder = MediaSortOrder.Default;
    public MediaSortOrder SelectedSortOrder
    {
        get => _selectedSortOrder;
        set { if (SetProperty(ref _selectedSortOrder, value)) _ = FilterMediaAsync(); }
    }

    public async Task RemoveFromContinueWatchingAsync(MediaProgress? progress)
    {
        if (progress is null || _disposed || _connectedProfileId is null || _contentStore is null) return;
        var updated = progress with { Completed = true, UpdatedAt = DateTimeOffset.UtcNow };
        await _contentStore.SaveProgressAsync(_connectedProfileId, updated);
        _progress = _progress.Where(p => p.Item.StorageKey != progress.Item.StorageKey).ToList();
        OnPropertyChanged(nameof(ContinueWatching));
        OnPropertyChanged(nameof(HasContinueWatching));
        NotifyHomeContent();
    }
    public string VersionLabel => typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "Candidato";
    public string HeaderSummary => ShowLiveCatalog ? CatalogSummary : ShowContinueWatching ? $"{ContinueWatching.Count} en curso" : $"{MediaItems.Count} títulos";
    public bool HasMovies => _movies?.Items.Count > 0;
    public bool HasSeries => _series?.Items.Count > 0;
    public bool HasNoMediaItems => ShowMediaBrowse && MediaItems.Count == 0 && !ContentBusy && !IsMediaFiltering;
    public string MediaEmptyMessage => HasMediaSearch || !string.IsNullOrEmpty(MediaCategory?.Id)
        ? "No encontramos títulos con estos filtros. Prueba otro nombre o selecciona Ver todos."
        : "No hay títulos guardados. Usa Películas/Series para cargar el catálogo de tu cuenta.";
    public bool IsMediaFiltering
    {
        get => _isMediaFiltering;
        private set { if (SetProperty(ref _isMediaFiltering, value)) OnPropertyChanged(nameof(HasNoMediaItems)); }
    }
    public bool ContentBusy { get => _contentBusy; private set { if (SetProperty(ref _contentBusy, value)) { OnPropertyChanged(nameof(IsWorking)); OnPropertyChanged(nameof(HasNoMediaItems)); OnPropertyChanged(nameof(CanPlayRandomEpisode)); } } }
    public string ContentStatus
    {
        get => _contentStatus;
        private set { if (SetProperty(ref _contentStatus, value)) OnPropertyChanged(nameof(FooterStatus)); }
    }
    public string EpgStatus
    {
        get => _epgStatus;
        private set
        {
            if (SetProperty(ref _epgStatus, value)) OnPropertyChanged(nameof(EpgPanelStatus));
        }
    }
    public string EpgPanelStatus => HasEpgSchedule
        ? string.Empty
        : EpgStatus == "Cargando guía…" ? "Cargando programación…" : "Programación no disponible";
    public string EpgNow { get; private set; } = "Sin programación actual.";
    public string EpgNext { get; private set; } = "Sin programación siguiente.";
    public IReadOnlyList<MediaItem> MediaItems { get; private set; } = [];
    public IReadOnlyList<MediaItem> VisibleMediaItems => MediaItems
        .Skip(_mediaPageIndex * MediaGridPageSize)
        .Take(MediaGridPageSize)
        .ToArray();
    private int _mediaPageIndex;
    public int MediaPageCount => Math.Max(1, (MediaItems.Count + MediaGridPageSize - 1) / MediaGridPageSize);
    public string MediaPageSummary => MediaItems.Count == 0
        ? "0 títulos"
        : $"{MediaItems.Count} títulos · página {_mediaPageIndex + 1} de {MediaPageCount}";
    public bool CanPreviousMediaPage => _mediaPageIndex > 0;
    public bool CanNextMediaPage => _mediaPageIndex + 1 < MediaPageCount;
    public void PreviousMediaPage() => ChangeMediaPage(_mediaPageIndex - 1);
    public void NextMediaPage() => ChangeMediaPage(_mediaPageIndex + 1);
    private void ChangeMediaPage(int page)
    {
        var next = Math.Clamp(page, 0, MediaPageCount - 1);
        if (next == _mediaPageIndex) return;
        _mediaPageIndex = next;
        NotifyMediaPaging();
    }
    private void NotifyMediaPaging()
    {
        OnPropertyChanged(nameof(VisibleMediaItems));
        OnPropertyChanged(nameof(MediaPageCount));
        OnPropertyChanged(nameof(MediaPageSummary));
        OnPropertyChanged(nameof(CanPreviousMediaPage));
        OnPropertyChanged(nameof(CanNextMediaPage));
    }
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
            foreach (var name in new[] { nameof(MediaDescription), nameof(MediaPosterUrl), nameof(MediaCategoryLabel), nameof(SeriesSummary), nameof(ShowMovieBrowse), nameof(ShowMovieDetails), nameof(ShowSeriesBrowse), nameof(ShowSeriesDetails), nameof(ShowMediaBrowse), nameof(ShowPlayerPane), nameof(ShowCatalogSplitter), nameof(NowPlaying), nameof(IsEpisodePlayerFocus), nameof(IsImmersiveMode), nameof(ImmersiveReturnLabel), nameof(CanPlayRandomEpisode) }) OnPropertyChanged(name);
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
                OnPropertyChanged(nameof(MediaEmptyMessage));
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
    public bool IsLivePlayback => (_playback.IsActive || _playback.State is PlaybackState.Playing or PlaybackState.Buffering or PlaybackState.Loading)
                                  && _watch is not null
                                  && _mediaWatch is null;
    public double DurationSeconds => _mediaDetails.LengthMilliseconds / 1000d;
    public double PositionSeconds => _mediaDetails.TimeMilliseconds / 1000d;
    public string PositionLabel => $"{TimeSpan.FromMilliseconds(_mediaDetails.TimeMilliseconds):hh\\:mm\\:ss} / {TimeSpan.FromMilliseconds(_mediaDetails.LengthMilliseconds):hh\\:mm\\:ss}";

    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private bool _isCatalogSyncing;
    public bool IsCatalogSyncing { get => _isCatalogSyncing; private set { if (SetProperty(ref _isCatalogSyncing, value)) { OnPropertyChanged(nameof(IsWorking)); OnPropertyChanged(nameof(HomeCatalogSkeletonVisible)); } } }
    private string? _catalogSyncStatus;
    public string? CatalogSyncStatus { get => _catalogSyncStatus; private set => SetProperty(ref _catalogSyncStatus, value); }

    public async Task<bool> AutoSyncCatalogAsync(bool force = false)
    {
        if (_disposed || ActiveProfile is null || ActiveProfile.ProviderType == "m3u" || !CanConnect) return false;
        if (!_syncGate.Wait(0)) return false;
        var version = _sessionVersion;
        CancellationTokenSource? operation = null;
        try
        {
            if (!force && _movies is not null && _movies.Items.Count > 0 && _series is not null && _series.Items.Count > 0)
            {
                var latestSync = _movies.SyncedAt > _series.SyncedAt ? _movies.SyncedAt : _series.SyncedAt;
                if (DateTimeOffset.UtcNow - latestSync < TimeSpan.FromHours(24))
                {
                    return true;
                }
            }

            IsCatalogSyncing = true;
            CatalogSyncStatus = "Sincronizando catálogo en segundo plano…";
            ContentBusy = true;

            if (_client is null) await ConnectProfileAsync();
            if (version != _sessionVersion || _disposed) return false;
            if (_client is not IContentProvider provider)
            {
                CatalogSyncStatus = "Tu cuenta no incluye películas ni series.";
                ContentStatus = CatalogSyncStatus;
                return false;
            }

            operation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
            _operation = operation;
            var failed = false;

            foreach (var kind in new[] { ContentKind.Movie, ContentKind.Series })
            {
                try
                {
                    CatalogSyncStatus = kind == ContentKind.Movie ? "Actualizando películas…" : "Actualizando series…";
                    ContentStatus = CatalogSyncStatus;
                    var catalog = await provider.GetMediaCatalogAsync(kind, operation.Token);
                    operation.Token.ThrowIfCancellationRequested();
                    if (version != _sessionVersion) return false;
                    if (_connectedProfileId is { } id && _contentStore is not null)
                    {
                        await _contentStore.SaveCatalogAsync(id, kind, "", catalog, operation.Token);
                    }
                    if (version != _sessionVersion) return false;
                    if (kind == ContentKind.Movie) _movies = catalog; else _series = catalog;
                    NotifyContent();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { failed = true; _logger.LogError("content_auto_sync", error); }
            }

            if (version == _sessionVersion && !_disposed)
            {
                ContentStatus = $"Películas: {_movies?.Items.Count ?? 0} · Series: {_series?.Items.Count ?? 0}" + (failed ? " · Se conserva la lista guardada." : "");
                CatalogSyncStatus = failed ? "Sincronización parcial: catálogo guardado disponible." : "Catálogo actualizado.";
                NotifyContent();
                await FilterMediaAsync();
            }

            return !failed;
        }
        catch (OperationCanceledException)
        {
            if (version == _sessionVersion) CatalogSyncStatus = "Sincronización cancelada.";
            return false;
        }
        catch (Exception error)
        {
            _logger.LogError("auto_sync_catalog", error);
            if (version == _sessionVersion) CatalogSyncStatus = "No se pudo actualizar el catálogo.";
            return false;
        }
        finally
        {
            if (ReferenceEquals(_operation, operation)) _operation = null;
            operation?.Dispose();
            ContentBusy = false;
            IsCatalogSyncing = false;
            _syncGate.Release();
        }
    }

    public async Task ProbeContentAsync()
    {
        await AutoSyncCatalogAsync(true);
    }

    private async Task LoadContentCacheAsync(string? profile, int version, CancellationToken token)
    {
        if (profile is null || _contentStore is null) return;
        var movies = await _contentStore.LoadCatalogAsync(profile, ContentKind.Movie, "", true, token);
        var series = await _contentStore.LoadCatalogAsync(profile, ContentKind.Series, "", true, token);
        var progress = await _contentStore.LoadProgressAsync(profile, token);
        if (version != _sessionVersion || _disposed) return;
        ContentStatus = $"Películas: {movies?.Items.Count ?? 0} · Series: {series?.Items.Count ?? 0}";
        _movies = movies; _series = series; _progress = progress; NotifyContent(); await FilterMediaAsync();
    }

    private async Task FilterMediaAsync(bool debounce = false)
    {
        if (_disposed) return;
        _mediaFilter?.Cancel(); using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _mediaFilter = request;
        var tab = CurrentTabIndex; var version = _sessionVersion;
        var catalog = tab == 6 ? _series : _movies; var query = MediaSearch; var category = MediaCategory?.Id;
        var favoriteIds = MediaFavoritesOnly
            ? (tab == 6 ? _library.SeriesFavorites : _library.MovieFavorites)
            : null;
        var index = tab == 6 ? _seriesIndex : _movieIndex;
        var restrictedCatIds = _parentalControl?.IsLocked == true
            ? (catalog?.Categories ?? []).Where(c => _parentalControl.IsRestrictedCategory(c.Name)).Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        try
        {
            IsMediaFiltering = ShowMediaCatalog;
            if (debounce) await Task.Delay(120, request.Token);
            var matches = await Task.Run(() =>
            {
                if (catalog is null) return (IReadOnlyList<MediaItem>)Array.Empty<MediaItem>();
                if (!ReferenceEquals(index?.Catalog, catalog)) index = new MediaCatalogSearch(catalog, request.Token);
                var filtered = index!.Filter(query, category, restrictedCatIds, request.Token);
                return favoriteIds is null ? filtered : filtered.Where(item => favoriteIds.Contains(item.Id)).ToArray();
            }, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_mediaFilter, request) || _disposed || version != _sessionVersion || tab != CurrentTabIndex) return;
            if (tab == 6) _seriesIndex = index; else _movieIndex = index;
            OnPropertyChanged(nameof(MediaEmptyMessage));
            if (SelectedSortOrder == MediaSortOrder.TitleAsc)
                matches = matches.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            else if (SelectedSortOrder == MediaSortOrder.TitleDesc)
                matches = matches.OrderByDescending(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            MediaItems = matches;
            _mediaPageIndex = 0;
            OnPropertyChanged(nameof(MediaItems));
            NotifyMediaPaging();
            OnPropertyChanged(nameof(HasNoMediaItems)); OnPropertyChanged(nameof(HeaderSummary));
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_filter", error); }
        finally { if (ReferenceEquals(_mediaFilter, request)) { _mediaFilter = null; IsMediaFiltering = false; } }
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
                if (cached is not null && cached.Items.Count == 0) cached = null;
                var rawEpisodes = cached ?? await provider.GetEpisodesAsync(item.Id, request.Token);
                var episodes = rawEpisodes with { Items = rawEpisodes.Items.Where(IsPlayableEpisode).ToArray() };
                request.Token.ThrowIfCancellationRequested(); if (!ReferenceEquals(_selectedMedia, item) || version != _sessionVersion) return;
                if (cached is null && episodes.Items.Count > 0 && _contentStore is not null && _connectedProfileId is { } id) await _contentStore.SaveCatalogAsync(id, ContentKind.Episode, item.Id, episodes, request.Token);
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

    public Task PlayMediaAsync(bool restart = false) => PlayMediaItemAsync(
        ShowContinueWatching
            ? SelectedProgress?.Item
            : (SelectedMedia?.Kind == ContentKind.Series || (SelectedMedia is null && SelectedEpisode is not null))
                ? SelectedEpisode
                : _selectedDetails ?? SelectedMedia,
        restart);
    private async Task PlayMediaItemAsync(MediaItem? item, bool restart = false)
    {
        if (item is null || _disposed || IsBusy || IsCatalogLoading) { if (item is null) ContentStatus = "Elige una película o episodio."; return; }
        CancelNextEpisodeAutoPlay();
        CloseAudioSubtitles();
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
            NotifyPlayerContext();

            if (_contentStore is not null && _connectedProfileId is not null)
            {
                var contentKey = item.Kind == ContentKind.Episode ? (item.SeriesId ?? item.CategoryId ?? item.Id) : item.Id;
                _mediaWatch.ContentTrackPreference = await _contentStore.LoadTrackPreferenceAsync(_connectedProfileId, contentKey, item.Kind, _session.Token);
            }

            if (item.Kind == ContentKind.Episode && !string.IsNullOrWhiteSpace(item.SeriesId) &&
                (_episodes is null || !_episodes.Items.Any(e => e.SeriesId == item.SeriesId)))
            {
                _ = EnsureEpisodesLoadedForSeriesAsync(item.SeriesId);
            }

            _playbackService.Options = _playbackService.Options with { OnDemand = true, StartPositionMilliseconds = position };
            if (item.Kind == ContentKind.Episode) _randomCyclePlayedKeys.Add(item.StorageKey);
            IsImmersiveMode = true;
            OnPropertyChanged(nameof(NowPlaying)); OnPropertyChanged(nameof(IsEpisodePlayerFocus)); OnPropertyChanged(nameof(IsImmersiveMode)); OnPropertyChanged(nameof(ImmersiveReturnLabel));
            OnPropertyChanged(nameof(CanBrowseEpisodes));
            if (IsEpisodesDrawerOpen) RefreshDrawerEpisodes();
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
    public async Task RefreshPlaybackDetailsAsync(bool periodicTimer = false)
    {
        RefreshPlaybackDiagnostics();
        UpdateEpgLabels();
        if (_disposed || !_playbackService.HasActiveMedia) return;
        if (_mediaWatch is not null) { await CaptureMediaAsync(); return; }
        var activeWatch = _watch;
        if (periodicTimer && activeWatch is not null && activeWatch.LiveDetailsLoaded && _playback.State == PlaybackState.Playing)
            return;
        var version = _sessionVersion; var channel = _playingChannelId;
        try
        {
            var details = await _playbackService.ReadDetailsAsync(_session.Token);
            if (!_disposed && version == _sessionVersion && channel == _playingChannelId && _mediaWatch is null)
            {
                _mediaDetails = details;
                if (activeWatch is not null && _playback.State == PlaybackState.Playing && details.AudioTracks.Count > 0)
                    activeWatch.LiveDetailsLoaded = true;
                NotifyMediaDetails();
            }
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
            if (!_disposed && watch.Version == _sessionVersion)
            {
                OnPropertyChanged(nameof(ContinueWatching));
                NotifyHomeContent();
            }
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
    private MediaPlaybackDetails _notifiedMediaDetails = MediaPlaybackDetails.Empty;
    private void NotifyMediaDetails()
    {
        var previous = _notifiedMediaDetails;
        var audioChanged = !previous.AudioTracks.SequenceEqual(_mediaDetails.AudioTracks);
        var subtitlesChanged = !previous.SubtitleTracks.SequenceEqual(_mediaDetails.SubtitleTracks);
        _mediaDetails = _mediaDetails with
        {
            AudioTracks = audioChanged ? _mediaDetails.AudioTracks : previous.AudioTracks,
            SubtitleTracks = subtitlesChanged ? _mediaDetails.SubtitleTracks : previous.SubtitleTracks
        };
        if (audioChanged) OnPropertyChanged(nameof(AudioTracks));
        if (subtitlesChanged) OnPropertyChanged(nameof(SubtitleTracks));
        if (audioChanged || previous.AudioTrack != _mediaDetails.AudioTrack)
        {
            SelectedAudioTrack = AudioTracks.FirstOrDefault(item => item.Id == _mediaDetails.AudioTrack);
            OnPropertyChanged(nameof(SelectedAudioTrack));
        }
        if (subtitlesChanged || previous.SubtitleTrack != _mediaDetails.SubtitleTrack)
        {
            SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(item => item.Id == _mediaDetails.SubtitleTrack);
            OnPropertyChanged(nameof(SelectedSubtitleTrack));
        }
        OnPropertyChanged(nameof(CanSeek));
        OnPropertyChanged(nameof(IsLivePlayback));
        if (previous.LengthMilliseconds != _mediaDetails.LengthMilliseconds) OnPropertyChanged(nameof(DurationSeconds));
        if (previous.TimeMilliseconds != _mediaDetails.TimeMilliseconds) OnPropertyChanged(nameof(PositionSeconds));
        if (previous.TimeMilliseconds != _mediaDetails.TimeMilliseconds || previous.LengthMilliseconds != _mediaDetails.LengthMilliseconds) OnPropertyChanged(nameof(PositionLabel));
        _notifiedMediaDetails = _mediaDetails;

        UpdateFormattedTracks();

        var watch = _mediaWatch;
        if (watch is not null && !watch.TrackPreferenceApplied && (_mediaDetails.AudioTracks.Count > 0 || _mediaDetails.SubtitleTracks.Count > 0))
        {
            watch.TrackPreferenceApplied = true;
            _ = ApplyTrackPreferencesOnPlaybackStartedAsync(watch);
        }
    }
    private void NotifyContent()
    {
        SelectedProgress ??= ContinueWatching.FirstOrDefault();
        foreach (var name in new[] { nameof(HasMovies), nameof(HasSeries), nameof(HasNoMediaItems), nameof(MediaCategories), nameof(VisibleMediaCategories), nameof(MediaCategoryLabel), nameof(ContinueWatching), nameof(SelectedProgress), nameof(FavoriteMovies), nameof(FavoriteSeries), nameof(FavoriteMediaItems), nameof(HasResolvedFavoriteContent), nameof(UnresolvedFavoriteCount), nameof(HasMissingFavoriteContent), nameof(HasMissingFavoriteMedia), nameof(ShowFavoriteMediaGroup), nameof(HasResolvedSelectedFavoriteContent), nameof(HasMissingSelectedFavoriteContent), nameof(FavoriteEmptyTitle), nameof(FavoriteEmptyDescription) }) OnPropertyChanged(name);
        NotifyHomeContent();
        NotifyFavoriteMediaPaging();
    }
    private void NotifyHomeContent()
    {
        foreach (var name in new[]
        {
            nameof(HomeContinueWatching), nameof(HomeMovies), nameof(HomeSeries), nameof(HasHomeMovies), nameof(HasHomeSeries),
            nameof(HomeCatalogSkeletonVisible),
            nameof(HomeFavoriteChannels),
            nameof(HomeFeaturedMedia), nameof(HomeFeaturedProgress), nameof(HomeFeaturedChannel), nameof(HasHomeFeatured),
            nameof(HasHomeFeaturedDescription), nameof(HasHomeFeaturedArt), nameof(HomeFeaturedTitle), nameof(HomeFeaturedDescription),
            nameof(HomeFeaturedPosterUrl), nameof(HomeFeaturedTypeLabel), nameof(HomeFeaturedActionLabel), nameof(CanShowHomeFeaturedDetails),
            nameof(HomeEmptyDescription)
        }) OnPropertyChanged(name);
    }
    private void ResetContent()
    {
        _detailRequest?.Cancel(); _mediaFilter?.Cancel(); _epgRequest?.Cancel();
        _movies = _series = _episodes = null; _progress = []; _selectedMedia = _selectedEpisode = _selectedDetails = null; _season = _mediaCategory = null; _isImmersiveMode = false; _randomCyclePlayedKeys.Clear();
        _movieIndex = _seriesIndex = null; IsMediaFiltering = false;
        MediaItems = []; _mediaPageIndex = 0; _mediaDetails = MediaPlaybackDetails.Empty;
        ContentStatus = "Explora películas y series de tu cuenta."; EpgStatus = "Selecciona un canal para ver la programación.";
        NotifyContent(); NotifyMediaDetails(); OnPropertyChanged(nameof(MediaItems)); NotifyMediaPaging(); OnPropertyChanged(nameof(HasNoMediaItems)); OnPropertyChanged(nameof(SelectedMedia)); OnPropertyChanged(nameof(MediaDescription)); OnPropertyChanged(nameof(MediaPosterUrl)); OnPropertyChanged(nameof(MediaCategoryLabel)); OnPropertyChanged(nameof(SeriesSummary)); OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes));
        OnPropertyChanged(nameof(ShowMovieBrowse)); OnPropertyChanged(nameof(ShowMovieDetails)); OnPropertyChanged(nameof(ShowSeriesBrowse)); OnPropertyChanged(nameof(ShowSeriesDetails)); OnPropertyChanged(nameof(ShowMediaBrowse)); OnPropertyChanged(nameof(ShowPlayerPane)); OnPropertyChanged(nameof(NowPlaying)); OnPropertyChanged(nameof(IsEpisodePlayerFocus)); OnPropertyChanged(nameof(IsImmersiveMode)); OnPropertyChanged(nameof(ImmersiveReturnLabel)); OnPropertyChanged(nameof(CanPlayRandomEpisode));
        _epgPrograms = []; UpdateEpgLabels();
    }
    private sealed class MediaWatch(MediaItem item, string? profileId, int version, long afterSequence)
    {
        public MediaItem Item { get; } = item; public string? ProfileId { get; } = profileId; public int Version { get; } = version;
        public long AfterSequence { get; } = afterSequence;
        public bool Played, Completed, RandomAdvanceTriggered, AutoPlayTriggered, TrackPreferenceApplied;
        public long Position, Length; public DateTimeOffset LastSaved;
        public MediaTrackPreference? ContentTrackPreference;
    }
}
