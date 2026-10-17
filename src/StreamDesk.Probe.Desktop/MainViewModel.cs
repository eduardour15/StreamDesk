using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly PlaybackService _playbackService = new();
    private readonly CancellationTokenSource _shutdown = new();
    private XtreamApiClient? _client;
    private XtreamAccount? _account;
    private XtreamChannel? _selectedChannel;
    private string _baseUrl = string.Empty;
    private string _username = string.Empty;
    private string _statusMessage = "Introduce el servidor y las credenciales de una cuenta autorizada.";
    private string? _httpWarning;
    private string? _connectionSummary;
    private string? _selectedFormat;
    private bool _isBusy;
    private bool _isPlaybackReady;
    private bool _disposed;

    public MainViewModel()
    {
        _playbackService.PlaybackStarted += PlaybackStarted;
        _playbackService.PlaybackFailed += PlaybackFailed;
        try
        {
            _playbackService.Initialize();
            IsPlaybackReady = true;
        }
        catch
        {
            IsPlaybackReady = false;
            StatusMessage = "No se pudo iniciar LibVLC. La conexión al catálogo aún se puede probar.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<XtreamCategory> Categories { get; private set; } = [];
    public IReadOnlyList<XtreamChannel> Channels { get; private set; } = [];
    public ObservableCollection<string> AvailableFormats { get; } = [];

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public XtreamChannel? SelectedChannel
    {
        get => _selectedChannel;
        set => SetProperty(ref _selectedChannel, value);
    }

    public string? SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? HttpWarning
    {
        get => _httpWarning;
        private set
        {
            if (SetProperty(ref _httpWarning, value))
            {
                OnPropertyChanged(nameof(HasHttpWarning));
            }
        }
    }

    public bool HasHttpWarning => !string.IsNullOrWhiteSpace(HttpWarning);

    public string? ConnectionSummary
    {
        get => _connectionSummary;
        private set => SetProperty(ref _connectionSummary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsPlaybackReady
    {
        get => _isPlaybackReady;
        private set => SetProperty(ref _isPlaybackReady, value);
    }

    public LibVLCSharp.Shared.MediaPlayer? MediaPlayer => _playbackService.MediaPlayer;

    public async Task ConnectAsync(string baseUrl, string username, string password)
    {
        if (IsBusy || _disposed)
        {
            return;
        }

        IsBusy = true;
        StopPlayback();
        _client?.Dispose();
        _client = null;
        _account = null;
        ClearCatalog();
        ConnectionSummary = null;
        HttpWarning = null;
        XtreamApiClient? newClient = null;

        try
        {
            newClient = new XtreamApiClient(baseUrl, username, password);
            var account = await newClient.AuthenticateAsync(_shutdown.Token);
            _client = newClient;
            _account = account;
            HttpWarning = newClient.UsesUnencryptedTransport
                ? "Este servidor usa HTTP. El usuario y la contraseña pueden viajar sin cifrar. Consulta al proveedor si ofrece HTTPS."
                : null;
            ConnectionSummary = FormatAccount(account);
            StatusMessage = "Autenticación aceptada. Carga el catálogo para continuar.";
            newClient = null;
        }
        catch (XtreamApiException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La solicitud fue cancelada.";
        }
        catch
        {
            StatusMessage = "No se pudo completar la conexión. Revisa los datos y vuelve a intentarlo.";
        }
        finally
        {
            newClient?.Dispose();
            IsBusy = false;
        }
    }

    public async Task LoadCatalogAsync()
    {
        if (IsBusy || _disposed)
        {
            return;
        }

        if (_client is null || _account is null)
        {
            StatusMessage = "Primero conecta una cuenta válida.";
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Consultando categorías…";
            var categories = await _client.GetLiveCategoriesAsync(_shutdown.Token);
            StatusMessage = "Consultando canales…";
            var channels = await _client.GetLiveChannelsAsync(_shutdown.Token);

            Categories = categories;
            OnPropertyChanged(nameof(Categories));
            Channels = channels;
            OnPropertyChanged(nameof(Channels));

            AvailableFormats.Clear();
            foreach (var format in _account.AllowedOutputFormats)
            {
                AvailableFormats.Add(format);
            }

            SelectedFormat = AvailableFormats.FirstOrDefault();
            StatusMessage = $"Catálogo recibido: {categories.Count} categorías y {channels.Count} canales.";
            if (AvailableFormats.Count == 0)
            {
                StatusMessage += " El proveedor no declaró formatos de salida compatibles; reproducción deshabilitada.";
            }
        }
        catch (XtreamApiException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La solicitud fue cancelada.";
        }
        catch
        {
            StatusMessage = "No se pudo cargar el catálogo. Revisa la conexión y vuelve a intentarlo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void PlaySelected()
    {
        if (_client is null || _account is null)
        {
            StatusMessage = "Primero conecta una cuenta válida.";
            return;
        }

        if (SelectedChannel is null)
        {
            StatusMessage = "Selecciona un canal para reproducir.";
            return;
        }

        if (!_playbackService.HasActiveMedia &&
            _account.MaxConnections is int maxConnections && maxConnections > 0 &&
            _account.ActiveConnections is int activeConnections && activeConnections >= maxConnections)
        {
            StatusMessage = "La cuenta ya alcanzó el límite de conexiones que informó el proveedor.";
            return;
        }

        if (!IsPlaybackReady)
        {
            StatusMessage = "LibVLC no está disponible en este equipo.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedFormat))
        {
            StatusMessage = "El proveedor no declaró un formato de salida compatible.";
            return;
        }

        try
        {
            var streamUri = _client.CreateLiveStreamUri(SelectedChannel, SelectedFormat);
            _playbackService.Play(streamUri);
            StatusMessage = "Solicitud enviada a LibVLC. Confirma visualmente que el canal autorizado inicia.";
        }
        catch (XtreamApiException exception)
        {
            StatusMessage = exception.Message;
        }
        catch
        {
            StatusMessage = "LibVLC no pudo iniciar el canal. Comprueba el acceso y el formato elegido.";
        }
    }

    public void StopPlayback()
    {
        _playbackService.Stop();
        StatusMessage = "Reproducción detenida y medio liberado.";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _playbackService.Stop();
        _playbackService.Dispose();
        _client?.Dispose();
        _client = null;
        _account = null;
        _shutdown.Dispose();
    }

    private void PlaybackStarted(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = "LibVLC reporta inicio de reproducción; valida imagen y audio.");

    private void PlaybackFailed(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(() => StatusMessage = "LibVLC no pudo abrir el canal. Comprueba autorización y formato.");

    private static string FormatAccount(XtreamAccount account)
    {
        var connections = account.ActiveConnections.HasValue || account.MaxConnections.HasValue
            ? $"Conexiones activas: {account.ActiveConnections?.ToString() ?? "desconocidas"} / límite {account.MaxConnections?.ToString() ?? "desconocido"}. "
            : "Límite de conexiones: el proveedor no lo declaró. ";
        var formats = account.AllowedOutputFormats.Count == 0
            ? "Formatos: no declarados."
            : $"Formatos: {string.Join(", ", account.AllowedOutputFormats)}.";
        return $"Estado: {account.Status}. {connections}{formats}";
    }

    private void ClearCatalog()
    {
        Categories = [];
        OnPropertyChanged(nameof(Categories));
        Channels = [];
        OnPropertyChanged(nameof(Channels));
        AvailableFormats.Clear();
        SelectedChannel = null;
        SelectedFormat = null;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
