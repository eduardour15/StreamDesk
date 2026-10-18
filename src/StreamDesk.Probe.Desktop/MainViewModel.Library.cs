using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly IViewerStore? _viewer;
    private ViewerLibrary _library = new(new HashSet<string>(), []);
    private Task _profileLoad = Task.CompletedTask, _historyWrites = Task.CompletedTask;
    private bool _startupEvaluated, _recentsOnly, _libraryBusy;
    private WatchRequest? _watch;
    private string? _catalogOwnerId;
    public bool ShowHome => CurrentTabIndex == 0;
    public bool ShowTv => CurrentTabIndex is >= 1 and <= 3;
    public bool ShowSettings => CurrentTabIndex == 4;
    public string SectionTitle => CurrentTabIndex switch { 2 => "Favoritos", 3 => "Recientes", _ => "TV en vivo" };
    public string ResultSummary => $"{Channels.Count} canales";
    public string EmptyMessage => _snapshot is null ? "Tu catálogo aparecerá aquí. Selecciona un perfil y pulsa Actualizar."
        : SearchText.Length > 0 ? "Sin resultados. Prueba otro nombre o limpia los filtros."
        : _recentsOnly ? "Aún no has visto canales de este perfil. Explora TV en vivo."
        : FavoritesOnly ? "Marca una estrella en TV en vivo para guardar tu primer favorito."
        : "Esta categoría no contiene canales. Prueba otra categoría.";
    public IReadOnlySet<string> FavoriteChannelIds => _snapshot?.FavoriteChannelIds ?? _library.Favorites;
    public bool CanFavorite => !_disposed && !_libraryBusy && _viewer is not null && ActiveProfile is not null && _catalogOwnerId == ActiveProfile.Id && _snapshot is not null;
    public bool SelectedIsFavorite => SelectedChannel is not null && FavoriteChannelIds.Contains(SelectedChannel.Id);
    public string FavoriteAction => SelectedIsFavorite ? "Quitar favorito" : "Añadir favorito";
    public bool AutoPlayLast => _library.AutoPlayLast;
    public XtreamChannel? LastChannel => _snapshot?.Channels.FirstOrDefault(item => item.Id == _library.Recents.FirstOrDefault()?.ChannelId);
    public bool HasLastChannel => LastChannel is not null;
    public string LastChannelLabel => LastChannel?.Name ?? "Todavía no has reproducido ningún canal";
    public IReadOnlyList<XtreamCategory> FrequentCategories { get; private set; } = [];

    public async Task ToggleFavoriteAsync(XtreamChannel? channel = null)
    {
        channel ??= SelectedChannel;
        if (!CanFavorite || channel is null) return;
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
        var library = await _viewer.LoadAsync(profileId, token);
        if (version != _sessionVersion || _disposed) return;
        _library = library;
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
    private void ResetLibrary() { _library = new(new HashSet<string>(), []); NotifyLibrary(); }
    private sealed class WatchRequest(string? profileId, XtreamChannel channel, int version, long afterSequence)
    {
        public string? ProfileId { get; } = profileId;
        public XtreamChannel Channel { get; } = channel;
        public int Version { get; } = version;
        public long AfterSequence { get; } = afterSequence;
        public bool Recorded { get; set; }
        public DateTimeOffset PlayedAt { get; set; }
    }
}
