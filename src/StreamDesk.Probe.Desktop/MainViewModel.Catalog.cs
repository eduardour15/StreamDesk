using System.Windows.Input;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly CatalogService _catalogService;
    private readonly ICredentialStore _credentials;
    private CancellationTokenSource _session = new();
    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _filter;
    private int _sessionVersion;
    private CatalogSnapshot? _snapshot;
    private CatalogSearch _search = new([]);
    private Profile? _activeProfile;
    private string? _connectedProfileId;
    public string? ConnectedProfileId => _connectedProfileId ?? (ActiveProfile?.ProviderType == "m3u" ? ActiveProfile.Id : null);
    private string _searchText = string.Empty;
    private string _categorySearch = string.Empty;
    private XtreamCategory? _selectedCategory;
    private bool _favoritesOnly;
    private bool _isCatalogLoading;
    private string _catalogSummary = "Elige un perfil o conecta tu lista.";

    public ICommand ConnectProfileCommand { get; }
    public Profile? ActiveProfile
    {
        get => _activeProfile;
        set { if (SetProperty(ref _activeProfile, value)) { OnPropertyChanged(nameof(ConnectedProfileId)); _profileLoad = Task.WhenAll(_profileLoad, LoadProfileCacheAsync(value)); } }
    }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearchText));
                _ = ApplyFilterAsync(true);
            }
        }
    }
    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);
    public string CategorySearch
    {
        get => _categorySearch;
        set
        {
            if (!SetProperty(ref _categorySearch, value)) return;
            OnPropertyChanged(nameof(HasCategorySearch));
            OnPropertyChanged(nameof(VisibleCategories));
        }
    }
    public bool HasCategorySearch => !string.IsNullOrWhiteSpace(CategorySearch);
    public IReadOnlyList<XtreamCategory> VisibleCategories
    {
        get
        {
            var allowedCategories = _parentalControl?.IsLocked == true
                ? Categories.Where(c => string.IsNullOrEmpty(c.Id) || !_parentalControl.IsRestrictedCategory(c.Name)).ToArray()
                : Categories;
            var query = CatalogSearch.Normalize(CategorySearch);
            if (query.Length == 0 || allowedCategories.Count == 0) return allowedCategories;
            var all = allowedCategories[0];
            return new[] { all }.Concat(allowedCategories.Skip(1).Where(category =>
                category.Id == SelectedCategory?.Id || CatalogSearch.Normalize(category.Name).Contains(query, StringComparison.Ordinal))).ToArray();
        }
    }
    public XtreamCategory? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) { OnPropertyChanged(nameof(VisibleCategories)); _ = ApplyFilterAsync(false); } } }
    public bool FavoritesOnly { get => _favoritesOnly; set { if (SetProperty(ref _favoritesOnly, value)) _ = ApplyFilterAsync(false); } }
    public string CatalogSummary { get => _catalogSummary; private set { if (SetProperty(ref _catalogSummary, value)) OnPropertyChanged(nameof(HeaderSummary)); } }
    public bool HasNoChannels => Channels.Count == 0 && !IsCatalogLoading;
    public bool IsWorking => IsBusy || IsCatalogLoading || ContentBusy;
    public bool IsCatalogLoading
    {
        get => _isCatalogLoading;
        private set
        {
            if (!SetProperty(ref _isCatalogLoading, value)) return;
            OnPropertyChanged(nameof(CanConnect)); OnPropertyChanged(nameof(CanImportPlaylist)); OnPropertyChanged(nameof(IsWorking)); OnPropertyChanged(nameof(HasNoChannels));
            NotifyCommands();
        }
    }

    private async Task LoadProfileCacheAsync(Profile? profile)
    {
        var version = BeginCatalogSession();
        _client?.Dispose(); _client = null; _account = null; _connectedProfileId = null;
        OnPropertyChanged(nameof(ConnectedProfileId)); OnPropertyChanged(nameof(IsConnected));
        ClearCatalog(); ConnectionSummary = null;
        _catalogOwnerId = profile?.Id;
        BaseUrl = profile?.BaseUrl ?? string.Empty; Username = string.Empty;
        HttpWarning = profile?.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? "Conexión sin cifrar (HTTP). Si tu proveedor ofrece HTTPS, es más seguro." : null;
        IsBusy = false; IsCatalogLoading = true;
        var token = _session.Token;
        try
        {
            await StopPlaybackAsync();
            token.ThrowIfCancellationRequested();
            if (profile?.ProviderType == "m3u" && _playlistImporter is not null)
            {
                var isHttp = profile.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             profile.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                var imported = isHttp && Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var uri)
                    ? await _playlistImporter.ImportRemoteAsync(uri, token)
                    : await _playlistImporter.ImportLocalAsync(profile.BaseUrl, token);
                if (version != _sessionVersion || _disposed) return;
                _playlistStreams = imported.StreamUris.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                _hasImportedPlaylist = true;
                await ApplySnapshotAsync(imported.Catalog, version, token);
                CatalogSummary = $"{imported.Catalog.Channels.Count} canales (M3U) · guardado";
                StatusMessage = "Lista M3U cargada.";
            }
            else
            {
                var cached = profile is null ? null : await _catalogService.LoadAsync(profile.Id, token);
                if (version != _sessionVersion || _disposed) return;
                if (cached is not null)
                {
                    await ApplySnapshotAsync(cached, version, token);
                    CatalogSummary = $"{cached.Channels.Count} canales · {cached.SyncedAt.LocalDateTime:g}";
                    StatusMessage = "Catálogo listo. Elige un canal para verlo.";
                }
                else CatalogSummary = "Sin canales guardados. Pulsa Actualizar canales.";
                await LoadLibraryAsync(profile?.Id, version, token);
                await LoadContentCacheAsync(profile?.Id, version, token);
            }
            if (profile is not null && _viewer is not null && version == _sessionVersion)
                await _viewer.SetLastProfileAsync(profile.Id, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _logger.LogError("catalog_cache_load", exception);
            if (version == _sessionVersion) CatalogSummary = "No se pudo abrir el catálogo guardado. Pulsa Actualizar.";
        }
        finally { if (version == _sessionVersion) IsCatalogLoading = false; }
    }

    public async Task ConnectAsync(string baseUrl, string username, string password)
    {
        if (!CanConnect || _disposed) return;
        BeginCatalogSession();
        ClearCatalog();
        CatalogSummary = "Sesión rápida activa. Guárdala en Ajustes si quieres conservarla.";
        await RunConnectionAsync(baseUrl, new(username, password), null);
    }

    public async Task ConnectProfileAsync()
    {
        if (!CanConnect || ActiveProfile is null || _disposed) return;
        var profile = ActiveProfile;
        await RunConnectionAsync(profile.BaseUrl, null, profile);
    }

    private async Task RunConnectionAsync(string baseUrl, StoredCredentials? credentials, Profile? profile)
    {
        var version = _sessionVersion;
        IsBusy = true;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
        _operation = operation;
        try
        {
            credentials ??= await _credentials.GetAsync(profile!.CredentialKey, operation.Token);
            if (credentials is null) { StatusMessage = "No se encontraron los datos de acceso de este perfil."; return; }
            await AuthenticateCoreAsync(baseUrl, credentials, profile?.Id, version, operation.Token);
        }
        catch (Exception exception) { ReportCatalogError(exception, version); }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; if (version == _sessionVersion) IsBusy = false; }
    }

    private async Task AuthenticateCoreAsync(string url, StoredCredentials credentials, string? profileId, int version, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (version != _sessionVersion || _disposed) return;
        await StopPlaybackAsync();
        token.ThrowIfCancellationRequested();
        if (version != _sessionVersion || _disposed) return;
        _client?.Dispose(); _client = null; _account = null; _connectedProfileId = null;
        AvailableFormats.Clear(); SelectedFormat = null; ConnectionSummary = null;
        IIptvProvider? provider = _providerFactory(url, credentials.Username, credentials.Password);
        try
        {
            HttpWarning = provider.UsesUnencryptedTransport
                ? "Conexión sin cifrar (HTTP). Si tu proveedor ofrece HTTPS, es más seguro." : null;
            var account = await provider.AuthenticateAsync(token);
            token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || _disposed) return;
            _client = provider; provider = null; _account = account; _connectedProfileId = profileId;
            OnPropertyChanged(nameof(ConnectedProfileId)); OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(ActiveConnectionsInfo)); OnPropertyChanged(nameof(AccountExpirationInfo));
            ConnectionSummary = FormatAccount(account);
            foreach (var format in account.AllowedOutputFormats) AvailableFormats.Add(format);
            SelectedFormat = AvailableFormats.FirstOrDefault();
            StatusMessage = account.HasReachedConnectionLimit
                ? "Conectado, pero tu cuenta ya está en uso en otro dispositivo."
                : "Conectado. Elige un canal o actualiza la lista.";
        }
        finally { provider?.Dispose(); }
    }

    public async Task LoadCatalogAsync()
    {
        if (!CanConnect || _disposed) return;
        var version = _sessionVersion;
        IsBusy = true;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
        _operation = operation;
        try
        {
            var token = operation.Token;
            if (_client is null)
            {
                var profile = ActiveProfile;
                if (profile is null) { StatusMessage = "Elige un perfil o conecta una cuenta primero."; return; }
                var credentials = await _credentials.GetAsync(profile.CredentialKey, token);
                if (credentials is null) { StatusMessage = "No se encontraron los datos guardados de este perfil."; return; }
                await AuthenticateCoreAsync(profile.BaseUrl, credentials, profile.Id, version, token);
            }
            token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || _client is null) return;
            StatusMessage = "Descargando canales…";
            var provider = _client;
            var snapshot = _connectedProfileId is string profileId
                ? await _catalogService.RefreshAsync(profileId, provider, token)
                : new CatalogSnapshot(await provider.GetLiveCategoriesAsync(token), await provider.GetLiveChannelsAsync(token), DateTimeOffset.UtcNow);
            if (version != _sessionVersion || _disposed) return;
            _catalogOwnerId = _connectedProfileId;
            await ApplySnapshotAsync(snapshot, version, token);
            if (version != _sessionVersion || _disposed) return;
            await LoadLibraryAsync(_connectedProfileId, version, token);
            if (version != _sessionVersion || _disposed) return;
            CatalogSummary = $"{snapshot.Channels.Count} canales · {snapshot.SyncedAt.LocalDateTime:g}";
            StatusMessage = snapshot.Channels.Count == 0 ? "Tu cuenta no devolvió canales." : "Canales actualizados.";
        }
        catch (Exception exception) { ReportCatalogError(exception, version); }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; if (version == _sessionVersion) IsBusy = false; }
    }

    private async Task ApplySnapshotAsync(CatalogSnapshot snapshot, int version, CancellationToken token)
    {
        var index = await Task.Run(() => new CatalogSearch(snapshot.Channels), token);
        token.ThrowIfCancellationRequested();
        if (version != _sessionVersion || _disposed) return;
        _snapshot = snapshot; _search = index;
        Categories = new[] { new XtreamCategory("", "Todas las categorías") }.Concat(snapshot.Categories).ToArray();
        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(VisibleCategories));
        _selectedCategory = Categories.FirstOrDefault(item => item.Id == _selectedCategory?.Id) ?? Categories[0];
        OnPropertyChanged(nameof(SelectedCategory));
        await ApplyFilterAsync(false);
        RefreshFrequentCategories();
    }

    private async Task ApplyFilterAsync(bool debounce)
    {
        if (_disposed) return;
        _filter?.Cancel();
        using var filter = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
        _filter = filter;
        var index = _search; var snapshot = _snapshot; var version = _sessionVersion;
        var query = SearchText; var category = SelectedCategory?.Id; var favoritesOnly = FavoritesOnly; var recentsOnly = _recentsOnly;
        var recentOrder = _library.Recents.Select((item, order) => (item.ChannelId, order)).ToDictionary(item => item.ChannelId, item => item.order);
        try
        {
            if (debounce) await Task.Delay(120, filter.Token);
            var result = await Task.Run(() =>
            {
                var matches = index.Filter(query, category, favoritesOnly, snapshot?.FavoriteChannelIds, filter.Token);
                if (_parentalControl?.IsLocked == true)
                {
                    var restrictedIds = Categories
                        .Where(c => !string.IsNullOrEmpty(c.Id) && _parentalControl.IsRestrictedCategory(c.Name))
                        .Select(c => c.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (restrictedIds.Count > 0)
                        matches = matches.Where(item => item.CategoryId is null || !restrictedIds.Contains(item.CategoryId)).ToArray();
                }
                return recentsOnly ? matches.Where(item => recentOrder.ContainsKey(item.Id)).OrderBy(item => recentOrder[item.Id]).ToArray() : matches;
            }, filter.Token);
            if (version != _sessionVersion || _disposed || !ReferenceEquals(_filter, filter)) return;
            var selectedId = SelectedChannel?.Id;
            Channels = result;
            OnPropertyChanged(nameof(Channels)); OnPropertyChanged(nameof(HasNoChannels)); OnPropertyChanged(nameof(ResultSummary)); OnPropertyChanged(nameof(EmptyMessage));
            SelectedChannel = result.FirstOrDefault(item => item.Id == selectedId);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { _logger.LogError("catalog_filter", exception); }
        finally { if (ReferenceEquals(_filter, filter)) _filter = null; }
    }

    private void ReportCatalogError(Exception exception, int version)
    {
        if (version != _sessionVersion || _disposed) return;
        StatusMessage = exception switch
        {
            OperationCanceledException => "Operación cancelada.",
            XtreamApiException apiError => apiError.Message,
            _ => "No se pudo conectar. Revisa tu internet e inténtalo de nuevo."
        };
        if (exception is not OperationCanceledException) _logger.LogError("catalog_operation", exception);
        if (_snapshot is not null) StatusMessage += " Se conserva el catálogo guardado.";
    }

    public void CancelCatalog() => _operation?.Cancel();
    private int BeginCatalogSession()
    {
        var version = ++_sessionVersion;
        _session.Cancel(); _session.Dispose(); _session = new();
        _operation?.Cancel(); _filter?.Cancel();
        ClearImportedPlaylist();
        return version;
    }
    private void DisposeCatalog()
    {
        _session.Cancel(); _operation?.Cancel(); _filter?.Cancel();
        _mediaFilter?.Cancel(); _detailRequest?.Cancel(); _epgRequest?.Cancel();
        _session.Dispose();
    }
}
