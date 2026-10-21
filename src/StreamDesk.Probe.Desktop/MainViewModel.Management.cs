using System.Collections.ObjectModel;
using System.Security.Cryptography;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private string _m3uProfileName = string.Empty;
    private string _m3uProfilePathOrUrl = string.Empty;
    private string _syncPassphrase = string.Empty;
    private string _syncStatus = string.Empty;
    private string _parentalStatus = string.Empty;
    private string _selectedTheme = "Dark";
    private string _selectedUiScale = "100%";
    private string _selectedLanguage = "es";

    public event Action<string>? ThemeChanged;
    public event Action<string>? UiScaleChanged;
    public event Action<string>? LanguageChanged;

    public IReadOnlyList<string> AvailableThemes { get; } = ["Dark", "Light"];
    public IReadOnlyList<string> AvailableUiScales { get; } = ["100%", "115%", "130%"];
    public IReadOnlyList<string> AvailableLanguages { get; } = ["Español", "English"];

    public string M3uProfileName { get => _m3uProfileName; set => SetProperty(ref _m3uProfileName, value); }
    public string M3uProfilePathOrUrl { get => _m3uProfilePathOrUrl; set => SetProperty(ref _m3uProfilePathOrUrl, value); }
    public string SyncPassphrase { get => _syncPassphrase; set => SetProperty(ref _syncPassphrase, value); }
    public string SyncStatus { get => _syncStatus; private set { if (SetProperty(ref _syncStatus, value)) OnPropertyChanged(nameof(HasSyncStatus)); } }
    public bool HasSyncStatus => !string.IsNullOrWhiteSpace(SyncStatus);

    public string ParentalStatus { get => _parentalStatus; private set { if (SetProperty(ref _parentalStatus, value)) OnPropertyChanged(nameof(HasParentalStatus)); } }
    public bool HasParentalStatus => !string.IsNullOrWhiteSpace(ParentalStatus);
    public bool IsParentalEnabled => _parentalControl?.IsEnabled ?? false;
    public bool IsParentalLocked => _parentalControl?.IsLocked ?? false;

    public bool IsConnected => _client is not null;
    public string ActiveConnectionsInfo => _account is null ? "Desconectado" : $"{_account.ActiveConnections ?? 0} de {_account.MaxConnections ?? 1} pantalla(s) en uso";
    public string AccountExpirationInfo => _account?.ExpiresAt is null ? "Ilimitada" : _account.ExpiresAt.Value.LocalDateTime.ToString("d");

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                ThemeChanged?.Invoke(value);
                _ = SavePreferencesAsync();
            }
        }
    }

    public string SelectedUiScale
    {
        get => _selectedUiScale;
        set
        {
            if (SetProperty(ref _selectedUiScale, value))
            {
                UiScaleChanged?.Invoke(value);
                _ = SavePreferencesAsync();
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                LanguageChanged?.Invoke(value);
                _ = SavePreferencesAsync();
            }
        }
    }

    public async Task DisconnectProfileAsync()
    {
        if (IsBusy || _disposed) return;
        await StopPlaybackAsync();
        _client?.Dispose();
        _client = null;
        _account = null;
        _connectedProfileId = null;
        ConnectionSummary = null;
        AvailableFormats.Clear();
        SelectedFormat = null;
        StatusMessage = "Desconectado. 0 conexiones activas con el servidor.";
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectedProfileId));
        OnPropertyChanged(nameof(ActiveConnectionsInfo));
        NotifyCommands();
    }

    public async Task CheckConnectionAsync()
    {
        if (IsBusy || ActiveProfile is null || _disposed) return;
        var profile = ActiveProfile;
        var version = _sessionVersion;
        IsBusy = true;
        StatusMessage = "Comprobando conexión y estado de cuenta…";
        try
        {
            var credentials = await _credentials.GetAsync(profile.CredentialKey, _shutdown.Token);
            if (credentials is null)
            {
                StatusMessage = "No se encontraron los datos de acceso para comprobar la cuenta.";
                return;
            }

            using var testClient = _providerFactory(profile.BaseUrl, credentials.Username, credentials.Password);
            var account = await testClient.AuthenticateAsync(_shutdown.Token);
            if (version != _sessionVersion || _disposed) return;

            _account = account;
            ConnectionSummary = FormatAccount(account);
            StatusMessage = account.HasReachedConnectionLimit
                ? $"Conexión verificada: límite alcanzado ({account.ActiveConnections}/{account.MaxConnections} en uso)."
                : $"Conexión verificada: cuenta activa ({account.ActiveConnections ?? 0}/{account.MaxConnections ?? 1} en uso).";

            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ActiveConnectionsInfo));
            OnPropertyChanged(nameof(AccountExpirationInfo));
        }
        catch (Exception exception)
        {
            _logger.LogError("check_connection", exception);
            StatusMessage = "No se pudo comprobar la conexión con el servidor.";
        }
        finally
        {
            if (version == _sessionVersion) IsBusy = false;
        }
    }

    public async Task<bool> CreateM3uProfileAsync()
    {
        if (_disposed || IsBusy) return false;
        SetProfileBusy(true);
        try
        {
            await _profilesService.CreateM3uProfileAsync(M3uProfileName, M3uProfilePathOrUrl, _shutdown.Token);
            M3uProfileName = string.Empty;
            M3uProfilePathOrUrl = string.Empty;
            ProfileStatus = "✓ Lista M3U guardada como perfil en este equipo.";
            await ReloadProfilesCoreAsync(_shutdown.Token);
            return true;
        }
        catch (ArgumentException exception) { ProfileStatus = exception.Message; return false; }
        catch (Exception exception)
        {
            _logger.LogError("m3u_profile_create", exception);
            ProfileStatus = "No se pudo guardar la lista M3U como perfil.";
            return false;
        }
        finally { SetProfileBusy(false); }
    }

    public async Task<bool> RenameProfileAsync(Profile profile, string newName)
    {
        if (profile is null || _disposed || IsBusy) return false;
        SetProfileBusy(true);
        try
        {
            await _profilesService.UpdateAsync(profile with { Name = newName }, _shutdown.Token);
            ProfileStatus = "✓ Perfil renombrado.";
            await ReloadProfilesCoreAsync(_shutdown.Token);
            return true;
        }
        catch (ArgumentException exception) { ProfileStatus = exception.Message; return false; }
        catch (Exception exception)
        {
            _logger.LogError("profile_rename", exception);
            ProfileStatus = "No se pudo renombrar el perfil.";
            return false;
        }
        finally { SetProfileBusy(false); }
    }

    public async Task<bool> ExportSyncAsync(string targetFilePath)
    {
        if (_syncService is null)
        {
            SyncStatus = "Servicio de sincronización no disponible.";
            return false;
        }
        if (ActiveProfile is null)
        {
            SyncStatus = "Selecciona un perfil activo antes de exportar.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(SyncPassphrase) || SyncPassphrase.Length < 4)
        {
            SyncStatus = "La contraseña debe tener al menos 4 caracteres.";
            return false;
        }

        try
        {
            var count = await _syncService.ExportAsync(targetFilePath, SyncPassphrase, ActiveProfile.Id, _shutdown.Token);
            SyncPassphrase = string.Empty;
            SyncStatus = $"✓ Paquete cifrado exportado ({count} elementos). Seguro para transferir entre tus PCs.";
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError("sync_export", exception);
            SyncStatus = "No se pudo exportar el paquete de sincronización.";
            return false;
        }
    }

    public async Task<bool> ImportSyncAsync(string sourceFilePath)
    {
        if (_syncService is null)
        {
            SyncStatus = "Servicio de sincronización no disponible.";
            return false;
        }
        if (ActiveProfile is null)
        {
            SyncStatus = "Selecciona un perfil activo antes de importar.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(SyncPassphrase))
        {
            SyncStatus = "Introduce la contraseña con la que fue cifrado el archivo.";
            return false;
        }

        try
        {
            var result = await _syncService.ImportAsync(sourceFilePath, SyncPassphrase, ActiveProfile.Id, _shutdown.Token);
            SyncPassphrase = string.Empty;
            SyncStatus = $"✓ {result.Message}";
            await LoadLibraryAsync(ActiveProfile.Id, _sessionVersion, _shutdown.Token);
            return true;
        }
        catch (CryptographicException)
        {
            SyncStatus = "Contraseña incorrecta o archivo de sincronización dañado.";
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError("sync_import", exception);
            SyncStatus = "No se pudo importar la sincronización.";
            return false;
        }
    }

    public async Task<bool> ConfigureParentalControlAsync(string pin)
    {
        if (_parentalControl is null)
        {
            ParentalStatus = "Control parental no disponible.";
            return false;
        }

        try
        {
            await _parentalControl.ConfigurePinAsync(pin, _shutdown.Token);
            ParentalStatus = "✓ Control parental activado con PIN.";
            NotifyParentalStateChanged();
            return true;
        }
        catch (ArgumentException exception)
        {
            ParentalStatus = exception.Message;
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError("parental_configure", exception);
            ParentalStatus = "No se pudo configurar el PIN parental.";
            return false;
        }
    }

    public async Task<bool> DisableParentalControlAsync(string pin)
    {
        if (_parentalControl is null) return false;
        try
        {
            var disabled = await _parentalControl.DisableAsync(pin, _shutdown.Token);
            if (disabled)
            {
                ParentalStatus = "Control parental desactivado.";
                NotifyParentalStateChanged();
                return true;
            }
            ParentalStatus = "PIN incorrecto.";
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError("parental_disable", exception);
            ParentalStatus = "No se pudo desactivar el control parental.";
            return false;
        }
    }

    public bool UnlockParental(string pin)
    {
        if (_parentalControl is null) return true;
        if (_parentalControl.Unlock(pin))
        {
            ParentalStatus = "✓ Contenido protegido desbloqueado temporalmente.";
            NotifyParentalStateChanged();
            return true;
        }
        ParentalStatus = "PIN incorrecto.";
        return false;
    }

    public void LockParental()
    {
        if (_parentalControl is null) return;
        _parentalControl.Lock();
        ParentalStatus = "Contenido protegido bloqueado.";
        NotifyParentalStateChanged();
    }

    private void NotifyParentalStateChanged()
    {
        OnPropertyChanged(nameof(IsParentalEnabled));
        OnPropertyChanged(nameof(IsParentalLocked));
        OnPropertyChanged(nameof(VisibleCategories));
        OnPropertyChanged(nameof(MediaCategories));
        OnPropertyChanged(nameof(VisibleMediaCategories));
        _ = ApplyFilterAsync(false);
        _ = FilterMediaAsync();
    }

    public async Task InitializeSettingsAsync()
    {
        if (_settingsService is null) return;
        try
        {
            var settings = await _settingsService.GetAppSettingsAsync(_shutdown.Token);
            _selectedTheme = settings.Theme;
            _selectedUiScale = settings.UiScale;
            _selectedLanguage = settings.Language;
            var clampedBuffer = Math.Clamp(settings.BufferMilliseconds, 250, 10000);
            var targetHardware = settings.SoftwareDecoding ? HardwareDecoding.Software : HardwareDecoding.Automatic;
            _playbackService.Options = (_playbackService.Options with { BufferMilliseconds = clampedBuffer, Hardware = targetHardware }).Validate();
            OnPropertyChanged(nameof(BufferMilliseconds));
            OnPropertyChanged(nameof(StabilityLabel));
            OnPropertyChanged(nameof(SoftwareDecoding));
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(SelectedUiScale));
            OnPropertyChanged(nameof(SelectedLanguage));
            ThemeChanged?.Invoke(_selectedTheme);
            UiScaleChanged?.Invoke(_selectedUiScale);
            LanguageChanged?.Invoke(_selectedLanguage);

            if (_parentalControl is not null)
            {
                await _parentalControl.InitializeAsync(_shutdown.Token);
                NotifyParentalStateChanged();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError("settings_init", exception);
        }
    }

    private async Task SavePreferencesAsync()
    {
        if (_settingsService is null || _disposed) return;
        try
        {
            var existing = await _settingsService.GetAppSettingsAsync(_shutdown.Token);
            var updated = existing with
            {
                Theme = SelectedTheme,
                UiScale = SelectedUiScale,
                Language = SelectedLanguage,
                BufferMilliseconds = Math.Clamp(BufferMilliseconds, 250, 10000),
                SoftwareDecoding = SoftwareDecoding
            };
            await _settingsService.SaveAppSettingsAsync(updated, _shutdown.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError("preferences_save", exception);
        }
    }
}
