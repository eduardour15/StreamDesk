using StreamDesk.Application;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private IReadOnlyDictionary<string, Uri> _playlistStreams = new Dictionary<string, Uri>();
    private HashSet<string> _playlistFavoriteIds = new(StringComparer.Ordinal);
    private string _remotePlaylistUrl = string.Empty;
    private string _playlistStatus = string.Empty;
    private bool _hasImportedPlaylist;

    public string RemotePlaylistUrl
    {
        get => _remotePlaylistUrl;
        set => SetProperty(ref _remotePlaylistUrl, value);
    }
    public string PlaylistStatus
    {
        get => _playlistStatus;
        private set
        {
            if (!SetProperty(ref _playlistStatus, value)) return;
            OnPropertyChanged(nameof(HasPlaylistStatus));
        }
    }
    public bool HasPlaylistStatus => !string.IsNullOrWhiteSpace(PlaylistStatus);
    public bool CanImportPlaylist => _playlistImporter is not null && CanConnect && !_disposed;
    public bool HasImportedPlaylist => _hasImportedPlaylist;

    public async Task ImportLocalPlaylistAsync(string path)
    {
        if (_playlistImporter is null || !CanImportPlaylist) return;
        await RunPlaylistImportAsync(token => _playlistImporter.ImportLocalAsync(path, token), isRemote: false);
    }

    public async Task ImportRemotePlaylistAsync(string sourceText)
    {
        if (_playlistImporter is null || !CanImportPlaylist) return;
        if (!Uri.TryCreate(sourceText?.Trim(), UriKind.Absolute, out var source))
        {
            PlaylistStatus = "Escribe una dirección HTTP o HTTPS válida.";
            RemotePlaylistUrl = string.Empty;
            return;
        }
        await RunPlaylistImportAsync(token => _playlistImporter.ImportRemoteAsync(source, token), isRemote: true);
    }

    private async Task RunPlaylistImportAsync(Func<CancellationToken, Task<ImportedPlaylist>> import, bool isRemote)
    {
        if (!CanImportPlaylist) return;
        var version = _sessionVersion;
        IsBusy = true;
        PlaylistStatus = "Importando lista…";
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
        _operation = operation;
        try
        {
            var imported = await import(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || _disposed) return;

            _filter?.Cancel();
            SearchText = string.Empty;
            FavoritesOnly = false;
            _recentsOnly = false;
            CurrentTabIndex = 1;
            _catalogOwnerId = null;
            _playlistStreams = imported.StreamUris.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            _playlistFavoriteIds = new HashSet<string>(StringComparer.Ordinal);
            _hasImportedPlaylist = true;
            OnPropertyChanged(nameof(HasImportedPlaylist));
            OnPropertyChanged(nameof(FavoriteChannelIds));
            OnPropertyChanged(nameof(CanFavorite));
            await ApplySnapshotAsync(imported.Catalog, version, operation.Token);
            if (version != _sessionVersion || _disposed) return;

            CatalogSummary = $"{imported.Catalog.Channels.Count} canales importados · temporal";
            PlaylistStatus = $"{imported.Catalog.Channels.Count} canales listos. Vuelve a importar la lista al reiniciar.";
            StatusMessage = "Lista M3U cargada. Sus direcciones se conservan solo en memoria.";
            ClearEpgForPlaylist();
        }
        catch (PlaylistImportException exception)
        {
            if (version == _sessionVersion && !_disposed) PlaylistStatus = exception.Message;
        }
        catch (OperationCanceledException)
        {
            if (version == _sessionVersion && !_disposed) PlaylistStatus = "Importación cancelada.";
        }
        catch (Exception)
        {
            if (version == _sessionVersion && !_disposed) PlaylistStatus = "No se pudo importar la lista.";
        }
        finally
        {
            if (isRemote) RemotePlaylistUrl = string.Empty;
            if (ReferenceEquals(_operation, operation)) _operation = null;
            if (version == _sessionVersion) IsBusy = false;
        }
    }

    private void ClearImportedPlaylist()
    {
        _playlistStreams = new Dictionary<string, Uri>();
        _playlistFavoriteIds.Clear();
        if (_hasImportedPlaylist)
        {
            _hasImportedPlaylist = false;
            OnPropertyChanged(nameof(HasImportedPlaylist));
        }
        PlaylistStatus = string.Empty;
        OnPropertyChanged(nameof(FavoriteChannelIds));
        OnPropertyChanged(nameof(CanFavorite));
    }
}
