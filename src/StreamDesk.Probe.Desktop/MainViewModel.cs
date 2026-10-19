using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel : INotifyPropertyChanged, IDisposable, IAsyncDisposable
{
    private readonly Func<string, string, string, IIptvProvider> _providerFactory;
    private readonly IPlaybackService _playbackService;
    private readonly ProfileService _profilesService;
    private readonly IAppLogger _logger;
    private readonly IPlaylistImporter? _playlistImporter;
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
    private string _profileStatus = "Tus cuentas se guardan cifradas en este equipo.";
    private string _statusMessage = "Listo · Puedes explorar tu lista guardada incluso sin internet.";
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
        ICredentialStore credentials,
        IViewerStore? viewer = null,
        IContentStore? content = null,
        IEpgImporter? epgImporter = null,
        IRecoveryService? recovery = null,
        IPlaylistImporter? playlistImporter = null)
    {
        _providerFactory = providerFactory;
        _playbackService = playbackService;
        _profilesService = profilesService;
        _logger = logger;
        _catalogService = catalogService;
        _credentials = credentials;
        _viewer = viewer;
        _contentStore = content;
        _epgImporter = epgImporter;
        _recovery = recovery;
        _playlistImporter = playlistImporter;
        LoadCatalogCommand = new AsyncRelayCommand(LoadCatalogAsync, () => CanConnect);
        ReloadProfilesCommand = new AsyncRelayCommand(LoadProfilesAsync, () => !IsBusy);
        DeleteProfileCommand = new AsyncRelayCommand(DeleteSelectedProfileAsync, () => !IsBusy && SelectedProfile is not null);
        ConnectProfileCommand = new AsyncRelayCommand(ConnectProfileAsync, () => CanConnect && ActiveProfile is not null);
        _playbackService.StateChanged += PlaybackStateChanged;
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
    public XtreamChannel? SelectedChannel { get => _selectedChannel; set { if (SetProperty(ref _selectedChannel, value)) NotifySelection(); } }
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
    public int CurrentTabIndex
    {
        get => _currentTabIndex;
        set
        {
            if (!SetProperty(ref _currentTabIndex, value)) return;
            if (value is >= 1 and <= 3)
            {
                _favoritesOnly = value == 2; _recentsOnly = value == 3;
                _searchText = ""; _selectedCategory = Categories.FirstOrDefault();
                OnPropertyChanged(nameof(FavoritesOnly)); OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(HasSearchText)); OnPropertyChanged(nameof(SelectedCategory));
                _ = ApplyFilterAsync(false);
            }
            OnPropertyChanged(nameof(ShowHome)); OnPropertyChanged(nameof(ShowTv)); OnPropertyChanged(nameof(ShowSettings)); OnPropertyChanged(nameof(SectionTitle));
            OnPropertyChanged(nameof(ShowLiveCatalog)); OnPropertyChanged(nameof(ShowMediaCatalog)); OnPropertyChanged(nameof(ShowContinueWatching));
            if (ShowMediaCatalog)
            {
                _mediaSearch = ""; _mediaCategory = null; SelectedMedia = null; MediaItems = [];
                OnPropertyChanged(nameof(MediaSearch)); OnPropertyChanged(nameof(HasMediaSearch)); OnPropertyChanged(nameof(MediaCategory)); OnPropertyChanged(nameof(MediaItems));
            }
            if (ShowContinueWatching && SelectedProgress is null)
            {
                SelectedProgress = ContinueWatching.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedProgress));
            }
            OnPropertyChanged(nameof(MediaCategories));
            OnPropertyChanged(nameof(HeaderSummary));
            _ = FilterMediaAsync();
        }
    }

    public async Task LoadProfilesAsync()
    {
        if (_disposed || IsBusy) return;
        var evaluateStartup = !_startupEvaluated;
        _startupEvaluated = true;
        var restored = false;
        SetProfileBusy(true);
        try
        {
            restored = await ReloadProfilesCoreAsync(_shutdown.Token);
            ProfileStatus = Profiles.Count == 0 ? "Agrega tu primera cuenta para empezar." : $"{Profiles.Count} cuenta(s) guardada(s).";
        }
        catch (Exception exception)
        {
            _logger.LogError("profile_load", exception);
            ProfileStatus = "No se pudieron leer las cuentas locales.";
        }
        finally { SetProfileBusy(false); }
        var startupVersion = _sessionVersion;
        await _profileLoad;
        if (evaluateStartup && restored && startupVersion == _sessionVersion)
        {
            if (!_disposed && AutoPlayLast && LastChannel is not null) await PlayLastAsync();
        }
    }

    public async Task<bool> CreateProfileAsync(string password)
    {
        if (_disposed || IsBusy) return false;
        SetProfileBusy(true);
        try
        {
            await _profilesService.CreateAsync(ProfileName, ProfileBaseUrl, ProfileUsername, password, _shutdown.Token);
            ProfileName = string.Empty;
            ProfileBaseUrl = string.Empty;
            ProfileUsername = string.Empty;
            ProfileStatus = "✓ Cuenta guardada de forma segura en este equipo.";
            await ReloadProfilesCoreAsync(_shutdown.Token);
            return true;
        }
        catch (ArgumentException exception) { ProfileStatus = exception.Message; return false; }
        catch (Exception exception)
        {
            _logger.LogError("profile_create", exception);
            ProfileStatus = "No se pudo guardar la cuenta. Revisa tus datos e inténtalo de nuevo.";
            return false;
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
            ProfileStatus = "Cuenta eliminada de este equipo.";
            await ReloadProfilesCoreAsync(_shutdown.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError("profile_delete", exception);
            ProfileStatus = "No se pudo eliminar la cuenta.";
        }
        finally { SetProfileBusy(false); }
    }

    public Task PlaySelectedAsync() => ShowMediaCatalog || ShowContinueWatching ? PlayMediaAsync() : PlayChannelAsync(SelectedChannel);
    public Task RetryPlaybackAsync() => !CanRetryPlayback ? Task.CompletedTask : _mediaWatch is { } media ? PlayMediaItemAsync(media.Item) : PlayChannelAsync(_watch?.Channel);

    private async Task PlayChannelAsync(XtreamChannel? channel)
    {
        if (IsBusy || IsCatalogLoading || _disposed) return;
        if (channel is null) { StatusMessage = "Haz clic en un canal para reproducir."; return; }
        var isPlaylistChannel = _playlistStreams.TryGetValue(channel.Id, out var playlistUri);
        var version = _sessionVersion;
        if (!isPlaylistChannel && _client is null && ActiveProfile is not null) await ConnectProfileAsync();
        if (_disposed || version != _sessionVersion) return;
        if (!isPlaylistChannel)
        {
            if (_client is null || _account is null) { StatusMessage = "Configura o selecciona una cuenta antes de reproducir."; return; }
            if (_account.ExpiresAt <= DateTimeOffset.UtcNow) { StatusMessage = new PlaybackException(PlaybackFailure.Expired).Message; return; }
            if (string.IsNullOrWhiteSpace(SelectedFormat)) { StatusMessage = "El servidor no indicó un formato de video compatible."; return; }
            if (!_playbackService.HasActiveMedia && _account.HasReachedConnectionLimit)
            { StatusMessage = "Tu cuenta alcanzó el límite de pantallas simultáneas."; return; }
        }
        try
        {
            if (isPlaylistChannel)
            {
                await _playlistImporter!.EnsureSafeStreamUriAsync(playlistUri!, _session.Token);
                if (_disposed || version != _sessionVersion) return;
            }
            await CaptureMediaAsync(true);
            if (_disposed || version != _sessionVersion) return;
            var source = isPlaylistChannel ? playlistUri! : _client!.CreateLiveStreamUri(channel, SelectedFormat!);
            _mediaWatch = null;
            _mediaDetails = MediaPlaybackDetails.Empty; NotifyMediaDetails();
            _playbackService.Options = _playbackService.Options with { OnDemand = false, StartPositionMilliseconds = 0 };
            _playingChannelId = channel.Id;
            _watch = new WatchRequest(isPlaylistChannel ? null : _connectedProfileId, channel, version, _playbackService.Snapshot.Sequence);
            OnPropertyChanged(nameof(NowPlaying));
            await _playbackService.PlayAsync(source, _session.Token);
            IsPlaybackReady = _playbackService.IsReady;
            OnPropertyChanged(nameof(PlayerHandle));
            if (isPlaylistChannel) ClearEpgForPlaylist();
            else _ = RefreshEpgAsync();
        }
        catch (OperationCanceledException) { }
        catch (PlaylistImportException exception) { StatusMessage = exception.Message; }
        catch (XtreamApiException exception) { StatusMessage = exception.Message; }
        catch (Exception exception)
        {
            _logger.LogError("playback_start", exception);
            StatusMessage = "No se pudo abrir el canal. Comprueba tu cuenta o prueba otro formato.";
        }
    }

    private async Task ShutdownCoreAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await CaptureMediaAsync(true);
        _playbackService.StateChanged -= PlaybackStateChanged;
        _shutdown.Cancel();
        DisposeCatalog();
        _client?.Dispose();
        _client = null;
        _account = null;
        try { await _profileLoad.ConfigureAwait(false); } catch { }
        await _historyWrites.ConfigureAwait(false);
        await _progressWrites.ConfigureAwait(false);
        try { await _playbackService.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { _logger.LogError("playback_dispose", exception); }
        finally { _shutdown.Dispose(); }
    }
    private Task? _shutdownTask;
    public ValueTask DisposeAsync() => new(_shutdownTask ??= ShutdownCoreAsync());
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static string FormatAccount(XtreamAccount account)
    {
        var connections = account.ActiveConnections.HasValue || account.MaxConnections.HasValue
            ? $"Pantallas: {account.ActiveConnections?.ToString() ?? "?"}/{account.MaxConnections?.ToString() ?? "?"} · "
            : "";
        var formats = account.AllowedOutputFormats.Count == 0 ? "" : $"Formato: {string.Join(", ", account.AllowedOutputFormats)}";
        return $"Cuenta activa · {connections}{formats}".TrimEnd(' ', '·');
    }

    private async Task<bool> ReloadProfilesCoreAsync(CancellationToken cancellationToken)
    {
        var activeId = ActiveProfile?.Id;
        var profiles = await _profilesService.GetProfilesAsync(cancellationToken);
        Profiles.Clear(); foreach (var profile in profiles) Profiles.Add(profile);
        var remembered = activeId ?? (_viewer is null ? null : await _viewer.GetLastProfileAsync(cancellationToken));
        var next = Profiles.FirstOrDefault(profile => profile.Id == ActiveProfile?.Id)
            ?? Profiles.FirstOrDefault(profile => profile.Id == remembered) ?? Profiles.FirstOrDefault();
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == (SelectedProfile?.Id ?? next?.Id)) ?? next;
        if (ActiveProfile?.Id == next?.Id) return false;
        ActiveProfile = next;
        return true;
    }

    private void ClearCatalog()
    {
        ClearImportedPlaylist();
        _snapshot = null;
        _catalogOwnerId = null;
        _search = new CatalogSearch([]);
        Categories = [];
        Channels = [];
        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(Channels));
        OnPropertyChanged(nameof(HasNoChannels));
        AvailableFormats.Clear(); SelectedChannel = null; SelectedFormat = null;
        ResetLibrary();
        ResetContent();
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
        OnPropertyChanged(nameof(CanImportPlaylist));
        NotifyCommands();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(propertyName); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
