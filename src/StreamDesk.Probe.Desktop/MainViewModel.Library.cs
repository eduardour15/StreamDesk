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
    public bool ShowTv => CurrentTabIndex is >= 1 and <= 3 or >= 5 and <= 7;
    public bool ShowSettings => CurrentTabIndex == 4;
    public string SectionTitle => CurrentTabIndex switch { 2 => "Favoritos", 3 => "Recientes", 5 => "Películas", 6 => "Series", 7 => "Continuar viendo", _ => "TV en vivo" };
    public string ResultSummary => $"{Channels.Count} canales";
    public string EmptyMessage => _snapshot is null ? "Pulsa Actualizar canales para cargar tu lista."
        : SearchText.Length > 0 ? "No encontramos ese canal. Prueba otro nombre."
        : _recentsOnly ? "Aún no has visto canales en este perfil."
        : FavoritesOnly ? "Pulsa ⭐ en cualquier canal para guardarlo aquí."
        : "No hay canales en esta categoría.";
    public IReadOnlySet<string> FavoriteChannelIds => _hasImportedPlaylist ? _playlistFavoriteIds : _snapshot?.FavoriteChannelIds ?? _library.Favorites;
    public bool CanFavorite => !_disposed && (_hasImportedPlaylist
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
        if (_hasImportedPlaylist)
        {
            if (_playlistFavoriteIds.Contains(channel.Id)) _playlistFavoriteIds.Remove(channel.Id);
            else _playlistFavoriteIds.Add(channel.Id);
            _snapshot = _snapshot! with { FavoriteChannelIds = new HashSet<string>(_playlistFavoriteIds, StringComparer.Ordinal) };
            OnPropertyChanged(nameof(FavoriteChannelIds));
            NotifySelection();
            await ApplyFilterAsync(false);
            StatusMessage = _playlistFavoriteIds.Contains(channel.Id) ? "Canal guardado en favoritos durante esta sesión." : "Canal eliminado de favoritos.";
            return;
        }
        var profileId = ActiveProfile!.Id; var version = _sessionVersion; var token = _session.Token;
        var add = !FavoriteChannelIds.Contains(channel.Id);
        _libraryBusy = true; OnPropertyChanged(nameof(CanFavorite));
        try
        {
            await _viewer!.SetFavoriteAsync(profileId, "live", channel.Id, add, token);
            if (version != _sessionVersion || _disposed) return;
            await LoadLibraryAsync(profileId, version, token);
            StatusMessage = add ? "Canal guardado en favoritos." : "Canal eliminado de favoritos.";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError("favorite_write", exception); if (version == _sessionVersion) StatusMessage = "No se pudo guardar el favorito. Vuelve a intentarlo."; }
        finally { _libraryBusy = false; OnPropertyChanged(nameof(CanFavorite)); NotifySelection(); OnPropertyChanged(nameof(FavoriteChannelIds)); }
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
        OnPropertyChanged(nameof(FavoriteChannelIds)); OnPropertyChanged(nameof(CanFavorite)); NotifySelection();
        OnPropertyChanged(nameof(AutoPlayLast)); OnPropertyChanged(nameof(LastChannel)); OnPropertyChanged(nameof(HasLastChannel)); OnPropertyChanged(nameof(LastChannelLabel));
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
