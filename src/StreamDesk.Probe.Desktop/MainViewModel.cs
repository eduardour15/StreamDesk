using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Func<string, string, string, IIptvProvider> _providerFactory;
    private readonly IPlaybackService _playbackService;
    private readonly ProfileService _profilesService;
    private readonly IAppLogger _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private IIptvProvider? _client;
    private XtreamAccount? _account;
    private XtreamChannel? _selectedChannel;
    private Profile? _selectedProfile;
    private string _baseUrl = string.Empty;
    private string _username = string.Empty;
    private string _profileName = string.Empty;
    private string _profileBaseUrl = string.Empty;
    private string _profileUsername = string.Empty;
    private string _profileStatus = "Los perfiles guardan sus credenciales cifradas para tu usuario de Windows.";
    private string _statusMessage = "StreamDesk puede iniciarse y navegar sin conexión.";
    private string? _httpWarning;
    private string? _connectionSummary;
    private string? _selectedFormat;
    private int _currentTabIndex;
    private bool _isBusy;
    private bool _isProfileBusy;
    private bool _isPlaybackReady;
    private bool _disposed;

    public MainViewModel(
        Func<string, string, string, IIptvProvider> providerFactory,
        IPlaybackService playbackService,
        ProfileService profilesService,
        IAppLogger logger,
        CatalogService catalogService,
        ICredentialStore credentials)
    {
        _providerFactory = providerFactory;
        _playbackService = playbackService;
        _profilesService = profilesService;
        _logger = logger;
        _catalogService = catalogService;
        _credentials = credentials;
        LoadCatalogCommand = new AsyncRelayCommand(LoadCatalogAsync, () => CanConnect);
        ReloadProfilesCommand = new AsyncRelayCommand(LoadProfilesAsync, () => !IsBusy);
        DeleteProfileCommand = new AsyncRelayCommand(DeleteSelectedProfileAsync, () => !IsBusy && SelectedProfile is not null);
        ConnectProfileCommand = new AsyncRelayCommand(ConnectProfileAsync, () => CanConnect && ActiveProfile is not null);
        _playbackService.PlaybackStarted += PlaybackStarted;
        _playbackService.PlaybackFailed += PlaybackFailed;
        try
        {
            _playbackService.Initialize();
            IsPlaybackReady = true;
        }
        catch (Exception exception)
        {
            _logger.LogError("playback_init", exception);
            StatusMessage = "No se pudo iniciar el motor de video. La navegación y configuración local siguen disponibles.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<XtreamCategory> Categories { get; private set; } = [];
    public IReadOnlyList<XtreamChannel> Channels { get; private set; } = [];
    public ObservableCollection<string> AvailableFormats { get; } = [];
    public ObservableCollection<Profile> Profiles { get; } = [];
    public ICommand LoadCatalogCommand { get; }
    public ICommand ReloadProfilesCommand { get; }
    public ICommand DeleteProfileCommand { get; }

    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string ProfileName { get => _profileName; set => SetProperty(ref _profileName, value); }
    public string ProfileBaseUrl { get => _profileBaseUrl; set => SetProperty(ref _profileBaseUrl, value); }
    public string ProfileUsername { get => _profileUsername; set => SetProperty(ref _profileUsername, value); }
    public string ProfileStatus { get => _profileStatus; private set => SetProperty(ref _profileStatus, value); }
    public Profile? SelectedProfile { get => _selectedProfile; set { if (SetProperty(ref _selectedProfile, value)) ((AsyncRelayCommand)DeleteProfileCommand).NotifyCanExecuteChanged(); } }
    public XtreamChannel? SelectedChannel { get => _selectedChannel; set => SetProperty(ref _selectedChannel, value); }
    public string? SelectedFormat { get => _selectedFormat; set => SetProperty(ref _selectedFormat, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string? HttpWarning { get => _httpWarning; private set { if (SetProperty(ref _httpWarning, value)) OnPropertyChanged(nameof(HasHttpWarning)); } }
    public bool HasHttpWarning => !string.IsNullOrWhiteSpace(HttpWarning);
    public string? ConnectionSummary { get => _connectionSummary; private set { if (SetProperty(ref _connectionSummary, value)) OnPropertyChanged(nameof(HasConnectionSummary)); } }
    public bool HasConnectionSummary => !string.IsNullOrWhiteSpace(ConnectionSummary);
    public bool IsBusy { get => _isBusy || _isProfileBusy; private set { if (_isBusy != value) { _isBusy = value; NotifyBusyChanged(); } } }
    public bool CanConnect => !IsBusy && !IsCatalogLoading;
    public bool IsPlaybackReady { get => _isPlaybackReady; private set => SetProperty(ref _isPlaybackReady, value); }
    public object? PlayerHandle => _playbackService.PlayerHandle;
    public int CurrentTabIndex { get => _currentTabIndex; set => SetProperty(ref _currentTabIndex, value); }

    public async Task LoadProfilesAsync()
    {
        if (_disposed || IsBusy) return;
        SetProfileBusy(true);
        try
        {
            await ReloadProfilesCoreAsync(_shutdown.Token);
            ProfileStatus = Profiles.Count == 0 ? "Todavía no hay perfiles guardados." : $"{Profiles.Count} perfil(es) disponibles.";
        }
        catch (Exception exception)
        {
            _logger.LogError("profile_load", exception);
            ProfileStatus = "No se pudieron cargar los perfiles locales.";
        }
        finally { SetProfileBusy(false); }
    }

    public async Task CreateProfileAsync(string password)
    {
        if (_disposed || IsBusy) return;
        SetProfileBusy(true);
        try
        {
            await _profilesService.CreateAsync(ProfileName, ProfileBaseUrl, ProfileUsername, password, _shutdown.Token);
            ProfileName = string.Empty;
            ProfileBaseUrl = string.Empty;
            ProfileUsername = string.Empty;
            ProfileStatus = "Perfil guardado. La contraseña se protegió con DPAPI CurrentUser.";
            await ReloadProfilesCoreAsync(_shutdown.Token);
        }
        catch (ArgumentException exception) { ProfileStatus = exception.Message; }
        catch (Exception exception)
        {
            _logger.LogError("profile_create", exception);
            ProfileStatus = "No se pudo guardar el perfil. Revisa la ruta de datos y vuelve a intentarlo.";
        }
        finally { SetProfileBusy(false); }
    }

    private async Task DeleteSelectedProfileAsync()
    {
        if (SelectedProfile is null || IsBusy || _disposed) return;
        SetProfileBusy(true);
        try
        {
            await _profilesService.DeleteAsync(SelectedProfile.Id, _shutdown.Token);
            SelectedProfile = null;
            ProfileStatus = "Perfil y credenciales eliminados.";
            await ReloadProfilesCoreAsync(_shutdown.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError("profile_delete", exception);
            ProfileStatus = "No se pudo eliminar el perfil y sus credenciales.";
        }
        finally { SetProfileBusy(false); }
    }

    public void PlaySelected()
    {
        if (IsBusy || IsCatalogLoading || _disposed) return;
        if (_client is null || _account is null) { StatusMessage = "Primero conecta una cuenta válida."; return; }
        if (SelectedChannel is null) { StatusMessage = "Selecciona un canal para reproducir."; return; }
        if (!_playbackService.IsReady) { StatusMessage = "LibVLC no está disponible en este equipo."; return; }
        if (string.IsNullOrWhiteSpace(SelectedFormat)) { StatusMessage = "El proveedor no declaró un formato compatible."; return; }
        if (!_playbackService.HasActiveMedia && _account.HasReachedConnectionLimit)
        { StatusMessage = "La cuenta ya alcanzó el límite de conexiones que informó el proveedor."; return; }
        try
        {
            _playbackService.Play(_client.CreateLiveStreamUri(SelectedChannel, SelectedFormat));
            StatusMessage = "Solicitud enviada a LibVLC. Confirma visualmente imagen y audio.";
        }
        catch (XtreamApiException exception) { StatusMessage = exception.Message; }
        catch (Exception exception)
        {
            _logger.LogError("playback_start", exception);
            StatusMessage = "LibVLC no pudo iniciar el canal. Comprueba autorización y formato.";
        }
    }

    public void StopPlayback() { _playbackService.Stop(); StatusMessage = "Reproducción detenida y medio liberado."; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown.Cancel();
        DisposeCatalog();
        _playbackService.PlaybackStarted -= PlaybackStarted;
        _playbackService.PlaybackFailed -= PlaybackFailed;
        _playbackService.Stop();
        _playbackService.Dispose();
        _client?.Dispose();
        _client = null;
        _account = null;
        _shutdown.Dispose();
    }

    private void PlaybackStarted(object? sender, EventArgs e) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = "LibVLC reporta reproducción; valida imagen y audio.");
    private void PlaybackFailed(object? sender, EventArgs e) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = "LibVLC no pudo abrir el canal. Comprueba autorización y formato.");

    private static string FormatAccount(XtreamAccount account)
    {
        var connections = account.ActiveConnections.HasValue || account.MaxConnections.HasValue
            ? $"Conexiones activas: {account.ActiveConnections?.ToString() ?? "desconocidas"} / límite {account.MaxConnections?.ToString() ?? "desconocido"}. "
            : "Límite de conexiones: el proveedor no lo declaró. ";
        var formats = account.AllowedOutputFormats.Count == 0 ? "Formatos: no declarados." : $"Formatos: {string.Join(", ", account.AllowedOutputFormats)}.";
        return $"Estado: {account.Status}. {connections}{formats}";
    }

    private async Task ReloadProfilesCoreAsync(CancellationToken cancellationToken)
    {
        var activeId = ActiveProfile?.Id;
        var profiles = await _profilesService.GetProfilesAsync(cancellationToken);
        Profiles.Clear(); foreach (var profile in profiles) Profiles.Add(profile);
        var next = Profiles.FirstOrDefault(profile => profile.Id == activeId) ?? Profiles.FirstOrDefault();
        if (ActiveProfile?.Id != next?.Id) ActiveProfile = next;
    }

    private void ClearCatalog()
    {
        _snapshot = null;
        _search = new CatalogSearch([]);
        Categories = [];
        Channels = [];
        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(Channels));
        OnPropertyChanged(nameof(HasNoChannels));
        AvailableFormats.Clear(); SelectedChannel = null; SelectedFormat = null;
    }

    private void NotifyCommands()
    {
        ((AsyncRelayCommand)LoadCatalogCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)ReloadProfilesCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)DeleteProfileCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)ConnectProfileCommand).NotifyCanExecuteChanged();
    }

    private void SetProfileBusy(bool value) { _isProfileBusy = value; NotifyBusyChanged(); }

    private void NotifyBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanConnect)); OnPropertyChanged(nameof(IsWorking));
        NotifyCommands();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(propertyName); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
