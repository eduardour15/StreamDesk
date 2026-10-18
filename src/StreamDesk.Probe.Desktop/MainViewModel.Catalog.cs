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
    private string _searchText = string.Empty;
    private XtreamCategory? _selectedCategory;
    private bool _favoritesOnly;
    private bool _isCatalogLoading;
    private string _catalogSummary = "Selecciona un perfil guardado o conecta una sesión temporal.";

    public ICommand ConnectProfileCommand { get; }
    public Profile? ActiveProfile
    {
        get => _activeProfile;
        set { if (SetProperty(ref _activeProfile, value)) _profileLoad = LoadProfileCacheAsync(value); }
    }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) _ = ApplyFilterAsync(true); } }
    public XtreamCategory? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) _ = ApplyFilterAsync(false); } }
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
            OnPropertyChanged(nameof(CanConnect)); OnPropertyChanged(nameof(IsWorking)); OnPropertyChanged(nameof(HasNoChannels));
            NotifyCommands();
        }
    }

    private async Task LoadProfileCacheAsync(Profile? profile)
    {
        var version = BeginCatalogSession();
        _client?.Dispose(); _client = null; _account = null; _connectedProfileId = null;
        ClearCatalog(); ConnectionSummary = null;
        _catalogOwnerId = profile?.Id;
        BaseUrl = profile?.BaseUrl ?? string.Empty; Username = string.Empty;
        HttpWarning = profile?.BaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? "Este servidor usa HTTP. Usuario y contraseña pueden viajar sin cifrar. Consulta si el proveedor ofrece HTTPS." : null;
        IsBusy = false; IsCatalogLoading = true;
        var token = _session.Token;
        try
        {
            await StopPlaybackAsync();
            token.ThrowIfCancellationRequested();
            var cached = profile is null ? null : await _catalogService.LoadAsync(profile.Id, token);
            if (version != _sessionVersion || _disposed) return;
            if (cached is not null)
            {
                await ApplySnapshotAsync(cached, version, token);
                CatalogSummary = $"Catálogo local · {cached.Channels.Count} canales · {cached.SyncedAt.LocalDateTime:g}";
                StatusMessage = "Metadatos disponibles sin conexión. Conecta o actualiza para reproducir.";
            }
            else CatalogSummary = "No hay catálogo guardado. Conecta y pulsa Actualizar catálogo.";
            await LoadLibraryAsync(profile?.Id, version, token);
            await LoadContentCacheAsync(profile?.Id, version, token);
            if (profile is not null && _viewer is not null && version == _sessionVersion)
                await _viewer.SetLastProfileAsync(profile.Id, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _logger.LogError("catalog_cache_load", exception);
            if (version == _sessionVersion) CatalogSummary = "No se pudo leer la caché. Puedes intentar actualizarla.";
        }
        finally { if (version == _sessionVersion) IsCatalogLoading = false; }
    }

    public async Task ConnectAsync(string baseUrl, string username, string password)
    {
        if (!CanConnect || _disposed) return;
        BeginCatalogSession();
        ClearCatalog();
        CatalogSummary = "Sesión temporal: guarda un perfil en Configuración para conservar su catálogo.";
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
            if (credentials is null) { StatusMessage = "Las credenciales de este perfil no están disponibles para tu usuario de Windows."; return; }
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
                ? "Este servidor usa HTTP. Usuario y contraseña pueden viajar sin cifrar. Consulta si hay HTTPS soportado." : null;
            var account = await provider.AuthenticateAsync(token);
            token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || _disposed) return;
            _client = provider; provider = null; _account = account; _connectedProfileId = profileId;
            ConnectionSummary = FormatAccount(account);
            foreach (var format in account.AllowedOutputFormats) AvailableFormats.Add(format);
            SelectedFormat = AvailableFormats.FirstOrDefault();
            StatusMessage = account.HasReachedConnectionLimit
                ? "Cuenta autenticada. El límite de conexiones está ocupado; puedes consultar el catálogo."
                : "Cuenta autenticada. Actualiza el catálogo o selecciona un canal de la caché.";
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
                if (profile is null) { StatusMessage = "Selecciona un perfil o conecta una sesión temporal."; return; }
                var credentials = await _credentials.GetAsync(profile.CredentialKey, token);
                if (credentials is null) { StatusMessage = "No se encontraron credenciales protegidas para este perfil."; return; }
                await AuthenticateCoreAsync(profile.BaseUrl, credentials, profile.Id, version, token);
            }
            token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || _client is null) return;
            StatusMessage = "Actualizando categorías y canales…";
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
            CatalogSummary = $"{(_connectedProfileId is null ? "Sesión temporal" : "Actualizado")} · {snapshot.Channels.Count} canales · {snapshot.SyncedAt.LocalDateTime:g}";
            StatusMessage = snapshot.Channels.Count == 0 ? "El proveedor devolvió un catálogo vacío." : "Catálogo actualizado. La búsqueda y los filtros funcionan sin conexión.";
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
            if (debounce) await Task.Delay(180, filter.Token);
            var result = await Task.Run(() =>
            {
                var matches = index.Filter(query, category, favoritesOnly, snapshot?.FavoriteChannelIds, filter.Token);
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
            OperationCanceledException => "Solicitud cancelada.",
            XtreamApiException apiError => apiError.Message,
            _ => "No se pudo completar la consulta. Revisa la conexión y vuelve a intentarlo."
        };
        if (exception is not OperationCanceledException) _logger.LogError("catalog_operation", exception);
        if (_snapshot is not null) StatusMessage += " Se conserva el catálogo conocido; puedes buscar o reintentar.";
    }

    public void CancelCatalog() => _operation?.Cancel();
    private int BeginCatalogSession()
    {
        var version = ++_sessionVersion;
        _session.Cancel(); _session.Dispose(); _session = new();
        _operation?.Cancel(); _filter?.Cancel();
        return version;
    }
    private void DisposeCatalog()
    {
        _session.Cancel(); _operation?.Cancel(); _filter?.Cancel(); _session.Dispose();
    }
}
