using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly IViewerStore? _viewer;
    private ViewerLibrary _library = new(new HashSet<string>(), []);
    private Task _profileLoad = Task.CompletedTask, _historyWrites = Task.CompletedTask, _preferenceWrites = Task.CompletedTask;
    private bool _startupEvaluated, _recentsOnly, _libraryBusy;
    private WatchRequest? _watch;
    private string? _catalogOwnerId;
    public bool ShowHome => CurrentTabIndex == 0;
    public bool ShowTv => CurrentTabIndex is 1 or 3 or >= 5 and <= 7;
    public bool ShowFavorites => CurrentTabIndex == 2;
    public bool ShowSettings => CurrentTabIndex == 4;
    public bool ShowCatalogSplitter => ShowLiveCatalog || ShowMovieDetails || ShowSeriesDetails || ShowContinueWatching;
    public string SectionTitle => CurrentTabIndex switch { 2 => "Favoritos", 3 => "Recientes", 5 => "Películas", 6 => "Series", 7 => "Continuar viendo", _ => "TV en vivo" };
    public string ResultSummary => $"{Channels.Count} canales";
    public string EmptyMessage => _snapshot is null ? "Pulsa Actualizar canales para cargar tu lista."
        : SearchText.Length > 0 ? "No encontramos ese canal. Prueba otro nombre."
        : _recentsOnly ? "Aún no has visto canales en este perfil."
        : FavoritesOnly ? "Pulsa ⭐ en cualquier canal para guardarlo aquí."
        : "No hay canales en esta categoría.";
    private bool UsesSessionPlaylistFavorites => _hasImportedPlaylist && (_catalogOwnerId is null || _viewer is null);
    public IReadOnlySet<string> FavoriteChannelIds => UsesSessionPlaylistFavorites ? _playlistFavoriteIds
        : _hasImportedPlaylist ? _library.M3uFavorites : _snapshot?.FavoriteChannelIds ?? _library.Favorites;
    public IReadOnlySet<string> MediaMovieFavoriteIds => _library.MovieFavorites;
    public IReadOnlySet<string> MediaSeriesFavoriteIds => _library.SeriesFavorites;
    public IReadOnlyList<XtreamChannel> FavoriteChannels => (_snapshot?.Channels ?? [])
        .Where(channel => FavoriteChannelIds.Contains(channel.Id))
        .OrderBy(channel => channel.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();
    public IReadOnlyList<XtreamChannel> HomeFavoriteChannels => (_snapshot?.Channels ?? [])
        .Where(channel => FavoriteChannelIds.Contains(channel.Id)).Take(8).ToArray();
    public bool HasHomeFavoriteChannels => HomeFavoriteChannels.Count > 0;
    public IReadOnlyList<MediaItem> FavoriteMovies => (_movies?.Items ?? [])
        .Where(item => _library.MovieFavorites.Contains(item.Id))
        .ToArray();
    public IReadOnlyList<MediaItem> FavoriteSeries => (_series?.Items ?? [])
        .Where(item => _library.SeriesFavorites.Contains(item.Id))
        .ToArray();
    public IReadOnlyList<MediaItem> FavoriteMediaItems => SelectedFavoriteTab switch
    {
        2 => FavoriteMovies,
        3 => FavoriteSeries,
        _ => FavoriteMovies.Concat(FavoriteSeries).ToArray()
    };
    private const int FavoriteMediaPageSize = 48;
    private int _favoriteMediaPageIndex;
    public IReadOnlyList<MediaItem> VisibleFavoriteMediaItems => FavoriteMediaItems.Skip(_favoriteMediaPageIndex * FavoriteMediaPageSize).Take(FavoriteMediaPageSize).ToArray();
    public int FavoriteMediaPageCount => Math.Max(1, (FavoriteMediaItems.Count + FavoriteMediaPageSize - 1) / FavoriteMediaPageSize);
    public string FavoriteMediaPageSummary => FavoriteMediaItems.Count == 0 ? "0 títulos" : $"{FavoriteMediaItems.Count} títulos · página {_favoriteMediaPageIndex + 1} de {FavoriteMediaPageCount}";
    public bool CanPreviousFavoriteMediaPage => _favoriteMediaPageIndex > 0;
    public bool CanNextFavoriteMediaPage => _favoriteMediaPageIndex + 1 < FavoriteMediaPageCount;
    public int FavoriteCount => FavoriteChannelIds.Count + _library.MovieFavorites.Count + _library.SeriesFavorites.Count;
    public bool HasFavoriteContent => FavoriteCount > 0;
    public bool HasResolvedFavoriteContent => FavoriteChannels.Count > 0 || FavoriteMovies.Count > 0 || FavoriteSeries.Count > 0;
    public int UnresolvedFavoriteCount => Math.Max(0, FavoriteCount - FavoriteChannels.Count - FavoriteMovies.Count - FavoriteSeries.Count);
    public bool HasMissingFavoriteContent => UnresolvedFavoriteCount > 0;
    public bool HasMissingFavoriteChannels => FavoriteChannelIds.Count > FavoriteChannels.Count;
    public bool HasMissingFavoriteMedia => _library.MovieFavorites.Count > FavoriteMovies.Count || _library.SeriesFavorites.Count > FavoriteSeries.Count;
    private int StoredFavoriteCountForTab => SelectedFavoriteTab switch
    {
        1 => FavoriteChannelIds.Count,
        2 => _library.MovieFavorites.Count,
        3 => _library.SeriesFavorites.Count,
        _ => FavoriteCount
    };
    private int ResolvedFavoriteCountForTab => SelectedFavoriteTab switch
    {
        1 => FavoriteChannels.Count,
        2 => FavoriteMovies.Count,
        3 => FavoriteSeries.Count,
        _ => FavoriteChannels.Count + FavoriteMovies.Count + FavoriteSeries.Count
    };
    public bool HasSelectedFavoriteContent => StoredFavoriteCountForTab > 0;
    public bool HasResolvedSelectedFavoriteContent => ResolvedFavoriteCountForTab > 0;
    public bool HasMissingSelectedFavoriteContent => StoredFavoriteCountForTab > ResolvedFavoriteCountForTab;
    public bool ShowFavoriteChannelsGroup => SelectedFavoriteTab == 1 || SelectedFavoriteTab == 0 && FavoriteChannels.Count > 0;
    public bool ShowFavoriteMediaGroup => SelectedFavoriteTab is 2 or 3 || SelectedFavoriteTab == 0 && FavoriteMediaItems.Count > 0;
    public string FavoriteEmptyTitle => HasFavoriteContent ? SelectedFavoriteTab switch
    {
        1 => "Todavía no tienes canales favoritos",
        2 => "Todavía no tienes películas favoritas",
        3 => "Todavía no tienes series favoritas",
        _ => "Todavía no tienes favoritos"
    } : "Todavía no tienes favoritos";
    public string FavoriteEmptyDescription => HasFavoriteContent
        ? "Guarda contenido de esta categoría para encontrarlo fácilmente aquí."
        : "Guarda tus canales, películas y series preferidas para encontrarlos fácilmente aquí.";
    public int SelectedFavoriteTab
    {
        get => _selectedFavoriteTab;
        set
        {
            if (!SetProperty(ref _selectedFavoriteTab, Math.Clamp(value, 0, 3))) return;
            OnPropertyChanged(nameof(IsFavoriteAllSelected));
            OnPropertyChanged(nameof(IsFavoriteChannelsSelected));
            OnPropertyChanged(nameof(IsFavoriteMoviesSelected));
            OnPropertyChanged(nameof(IsFavoriteSeriesSelected));
            OnPropertyChanged(nameof(FavoriteMediaItems));
            OnPropertyChanged(nameof(ShowFavoriteChannelsGroup));
            OnPropertyChanged(nameof(ShowFavoriteMediaGroup));
            OnPropertyChanged(nameof(HasSelectedFavoriteContent));
            OnPropertyChanged(nameof(HasResolvedSelectedFavoriteContent));
            OnPropertyChanged(nameof(HasMissingSelectedFavoriteContent));
            OnPropertyChanged(nameof(FavoriteEmptyTitle));
            OnPropertyChanged(nameof(FavoriteEmptyDescription));
            _favoriteMediaPageIndex = 0;
            NotifyFavoriteMediaPaging();
        }
    }
    private int _selectedFavoriteTab;
    public bool IsFavoriteAllSelected => SelectedFavoriteTab == 0;
    public bool IsFavoriteChannelsSelected => SelectedFavoriteTab == 1;
    public bool IsFavoriteMoviesSelected => SelectedFavoriteTab == 2;
    public bool IsFavoriteSeriesSelected => SelectedFavoriteTab == 3;
    public void PreviousFavoriteMediaPage() { if (_favoriteMediaPageIndex > 0) { _favoriteMediaPageIndex--; NotifyFavoriteMediaPaging(); } }
    public void NextFavoriteMediaPage() { if (CanNextFavoriteMediaPage) { _favoriteMediaPageIndex++; NotifyFavoriteMediaPaging(); } }
    public async Task RefreshFavoriteCatalogsAsync()
    {
        var tab = SelectedFavoriteTab;
        if (tab is 0 or 1 && HasMissingFavoriteChannels)
        {
            if (ActiveProfile?.ProviderType == "m3u") await LoadProfileCacheAsync(ActiveProfile);
            else await LoadCatalogAsync();
        }
        if (tab is 0 or 2 or 3 && HasMissingFavoriteMedia)
            await ProbeContentAsync();
    }
    private void NotifyFavoriteMediaPaging()
    {
        OnPropertyChanged(nameof(VisibleFavoriteMediaItems));
        OnPropertyChanged(nameof(FavoriteMediaPageCount));
        OnPropertyChanged(nameof(FavoriteMediaPageSummary));
        OnPropertyChanged(nameof(CanPreviousFavoriteMediaPage));
        OnPropertyChanged(nameof(CanNextFavoriteMediaPage));
    }
    public bool CanFavoriteMedia => !_disposed && !_libraryBusy && _viewer is not null && ActiveProfile is not null && _catalogOwnerId == ActiveProfile.Id;
    public bool CanFavorite => !_disposed && (UsesSessionPlaylistFavorites
        ? !IsBusy && !IsCatalogLoading && _snapshot is not null
        : !_libraryBusy && _viewer is not null && ActiveProfile is not null && _catalogOwnerId == ActiveProfile.Id && _snapshot is not null);
    public bool SelectedIsFavorite => SelectedChannel is not null && FavoriteChannelIds.Contains(SelectedChannel.Id);
    public string FavoriteAction => SelectedIsFavorite ? "★ Quitar favorito" : "☆ Favorito";
    public bool AutoPlayLast => _library.AutoPlayLast;
    public XtreamChannel? LastChannel => _snapshot?.Channels.FirstOrDefault(item => item.Id == _library.Recents.FirstOrDefault()?.ChannelId);
    public bool HasLastChannel => LastChannel is not null;
    public string LastChannelLabel => LastChannel?.Name ?? "Elige tu primer canal en TV en vivo";
    public IReadOnlyList<XtreamCategory> FrequentCategories { get; private set; } = [];

    public async Task ToggleFavoriteAsync(XtreamChannel? channel = null)
    {
        channel ??= SelectedChannel;
        if (!CanFavorite || channel is null) return;
        if (UsesSessionPlaylistFavorites)
        {
            if (_playlistFavoriteIds.Contains(channel.Id)) _playlistFavoriteIds.Remove(channel.Id);
            else _playlistFavoriteIds.Add(channel.Id);
            _snapshot = _snapshot! with { FavoriteChannelIds = new HashSet<string>(_playlistFavoriteIds, StringComparer.Ordinal) };
            OnPropertyChanged(nameof(FavoriteChannelIds));
            NotifySelection();
            NotifyHomeContent();
            await ApplyFilterAsync(false);
            StatusMessage = _playlistFavoriteIds.Contains(channel.Id) ? "Canal guardado en favoritos durante esta sesión." : "Canal eliminado de favoritos.";
            return;
        }
        var profileId = ActiveProfile!.Id; var version = _sessionVersion; var token = _session.Token;
        var add = !FavoriteChannelIds.Contains(channel.Id);
        var contentType = _hasImportedPlaylist ? "m3u-live" : "live";
        _libraryBusy = true; OnPropertyChanged(nameof(CanFavorite));
        try
        {
            await _viewer!.SetFavoriteAsync(profileId, contentType, channel.Id, add, token);
            if (version != _sessionVersion || _disposed) return;
            await LoadLibraryAsync(profileId, version, token);
            StatusMessage = add ? "Canal guardado en favoritos." : "Canal eliminado de favoritos.";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError("favorite_write", exception); if (version == _sessionVersion) StatusMessage = "No se pudo guardar el favorito. Vuelve a intentarlo."; }
        finally { _libraryBusy = false; OnPropertyChanged(nameof(CanFavorite)); NotifySelection(); OnPropertyChanged(nameof(FavoriteChannelIds)); }
    }

    public async Task ToggleMediaFavoriteAsync(MediaItem? item)
    {
        if (item is null || item.Kind is not (ContentKind.Movie or ContentKind.Series) || !CanFavoriteMedia) return;
        var profileId = ActiveProfile!.Id;
        var version = _sessionVersion;
        var type = item.Kind == ContentKind.Movie ? "movie" : "series";
        var favorites = item.Kind == ContentKind.Movie ? _library.MovieFavorites : _library.SeriesFavorites;
        var add = !favorites.Contains(item.Id);
        _libraryBusy = true;
        OnPropertyChanged(nameof(CanFavorite));
        OnPropertyChanged(nameof(CanFavoriteMedia));
        try
        {
            await _viewer!.SetFavoriteAsync(profileId, type, item.Id, add, _session.Token);
            if (version != _sessionVersion || _disposed) return;
            await LoadLibraryAsync(profileId, version, _session.Token);
            await FilterMediaAsync();
            if (version == _sessionVersion && !_disposed)
                StatusMessage = add ? "Título guardado en favoritos." : "Título eliminado de favoritos.";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _logger.LogError("media_favorite_write", exception);
            if (version == _sessionVersion && !_disposed) StatusMessage = "No se pudo guardar el favorito. Vuelve a intentarlo.";
        }
        finally
        {
            _libraryBusy = false;
            OnPropertyChanged(nameof(CanFavorite));
            OnPropertyChanged(nameof(CanFavoriteMedia));
            OnPropertyChanged(nameof(MediaMovieFavoriteIds));
            OnPropertyChanged(nameof(MediaSeriesFavoriteIds));
        }
    }

    public async Task SetAutoPlayAsync(bool enabled)
    {
        if (_viewer is null || ActiveProfile is null || _libraryBusy || _disposed) { OnPropertyChanged(nameof(AutoPlayLast)); return; }
        var id = ActiveProfile.Id; var version = _sessionVersion;
        _libraryBusy = true; OnPropertyChanged(nameof(CanFavorite));
        try
        {
            await _viewer.SetAutoPlayAsync(id, enabled, _session.Token);
            if (version == _sessionVersion && !_disposed) _library = _library with { AutoPlayLast = enabled };
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError("preference_write", exception); if (version == _sessionVersion && !_disposed) StatusMessage = "No se pudo guardar la preferencia de inicio."; }
        finally { _libraryBusy = false; OnPropertyChanged(nameof(AutoPlayLast)); OnPropertyChanged(nameof(CanFavorite)); }
    }

    public async Task PlayLastAsync()
    {
        if (LastChannel is not { } channel) return;
        CurrentTabIndex = 1;
        SelectedChannel = channel;
        await PlaySelectedAsync();
    }

    public void ClearFilters() { SearchText = ""; SelectedCategory = Categories.FirstOrDefault(); FavoritesOnly = CurrentTabIndex == 2; }

    private async Task LoadLibraryAsync(string? profileId, int version, CancellationToken token)
    {
        if (_viewer is null || profileId is null) return;
        try { await _preferenceWrites; } catch { }
        var library = await _viewer.LoadAsync(profileId, token);
        if (version != _sessionVersion || _disposed) return;
        _library = library;
        ApplyProfileAmplification(library.AmplificationDb);
        if (_snapshot is not null) _snapshot = _snapshot with { FavoriteChannelIds = library.Favorites };
        NotifyLibrary(); await ApplyFilterAsync(false);
    }

    private async Task RecordWatchAsync(Task previous, WatchRequest watch)
    {
        await previous;
        if (_viewer is null || watch.ProfileId is null) return;
        try
        {
            await _viewer.RecordPlayedAsync(watch.ProfileId, "live", watch.Channel.Id, watch.PlayedAt);
            if (!_disposed && watch.Version == _sessionVersion)
                await LoadLibraryAsync(watch.ProfileId, watch.Version, _session.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError("history_write", exception); if (!_disposed) StatusMessage = "La reproducción continúa, pero no se pudo guardar el reciente."; }
    }

    private void NotifySelection() { OnPropertyChanged(nameof(SelectedIsFavorite)); OnPropertyChanged(nameof(FavoriteAction)); }
    private void NotifyLibrary()
    {
        OnPropertyChanged(nameof(FavoriteChannelIds)); OnPropertyChanged(nameof(MediaMovieFavoriteIds)); OnPropertyChanged(nameof(MediaSeriesFavoriteIds));
        OnPropertyChanged(nameof(FavoriteChannels)); OnPropertyChanged(nameof(FavoriteMovies)); OnPropertyChanged(nameof(FavoriteSeries));
        OnPropertyChanged(nameof(FavoriteMediaItems)); OnPropertyChanged(nameof(FavoriteCount)); OnPropertyChanged(nameof(HasFavoriteContent));
        OnPropertyChanged(nameof(HasResolvedFavoriteContent)); OnPropertyChanged(nameof(UnresolvedFavoriteCount)); OnPropertyChanged(nameof(HasMissingFavoriteContent));
        OnPropertyChanged(nameof(HasMissingFavoriteChannels)); OnPropertyChanged(nameof(HasMissingFavoriteMedia));
        OnPropertyChanged(nameof(HasSelectedFavoriteContent)); OnPropertyChanged(nameof(HasResolvedSelectedFavoriteContent)); OnPropertyChanged(nameof(HasMissingSelectedFavoriteContent));
        OnPropertyChanged(nameof(ShowFavoriteChannelsGroup)); OnPropertyChanged(nameof(ShowFavoriteMediaGroup));
        OnPropertyChanged(nameof(FavoriteEmptyTitle)); OnPropertyChanged(nameof(FavoriteEmptyDescription));
        NotifyFavoriteMediaPaging();
        OnPropertyChanged(nameof(CanFavorite)); OnPropertyChanged(nameof(CanFavoriteMedia)); NotifySelection();
        OnPropertyChanged(nameof(AutoPlayLast)); OnPropertyChanged(nameof(LastChannel)); OnPropertyChanged(nameof(HasLastChannel)); OnPropertyChanged(nameof(LastChannelLabel));
        OnPropertyChanged(nameof(HomeFavoriteChannels));
        OnPropertyChanged(nameof(HasHomeFavoriteChannels));
        NotifyHomeContent();
        RefreshFrequentCategories();
    }
    private void RefreshFrequentCategories()
    {
        var channels = _snapshot?.Channels.ToDictionary(item => item.Id) ?? [];
        var counts = _library.Recents.Where(item => channels.ContainsKey(item.ChannelId)).Select(item => channels[item.ChannelId].CategoryId)
            .Where(id => id is not null).GroupBy(id => id!).ToDictionary(group => group.Key, group => group.Count());
        var populated = channels.Values.Select(item => item.CategoryId).ToHashSet();
        FrequentCategories = Categories.Where(item => item.Id.Length > 0 && populated.Contains(item.Id))
            .OrderByDescending(item => counts.GetValueOrDefault(item.Id)).ThenBy(item => item.Name).Take(4).ToArray();
        OnPropertyChanged(nameof(FrequentCategories));
    }
    private void ResetLibrary() { _library = new(new HashSet<string>(), []); ApplyProfileAmplification(0); NotifyLibrary(); }

    private void QueueAmplificationSave(int amplificationDb)
    {
        var clamped = Math.Clamp(amplificationDb, 0, PlaybackService.MaxAmplificationDb);
        _library = _library with { AmplificationDb = clamped };
        if (_viewer is null || ActiveProfile is not { Id: var profileId } || _disposed) return;
        _preferenceWrites = SaveAmplificationPreferenceAsync(_preferenceWrites, profileId, clamped);
    }

    private async Task SaveAmplificationPreferenceAsync(Task previous, string profileId, int amplificationDb)
    {
        try { await previous.ConfigureAwait(false); } catch { }
        if (_viewer is null) return;
        try { await _viewer.SetAmplificationAsync(profileId, amplificationDb).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError("amplification_write", exception); }
    }
    private sealed class WatchRequest(string? profileId, XtreamChannel channel, int version, long afterSequence)
    {
        public string? ProfileId { get; } = profileId;
        public XtreamChannel Channel { get; } = channel;
        public int Version { get; } = version;
        public long AfterSequence { get; } = afterSequence;
        public bool Recorded { get; set; }
        public bool TriedAlternateFormat { get; set; }
        public bool LiveDetailsLoaded { get; set; }
        public DateTimeOffset PlayedAt { get; set; }
    }
}
