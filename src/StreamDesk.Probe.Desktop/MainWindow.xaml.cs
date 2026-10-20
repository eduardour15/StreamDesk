using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using StreamDesk.Probe.Core;
using Wpf.Ui.Controls;

namespace StreamDesk.Probe.Desktop;

public partial class MainWindow : FluentWindow
{
    private const int WindowStyleIndex = -16;
    private const int SystemMenuStyle = 0x00080000;
    private const uint FrameChanged = 0x0020, NoMove = 0x0002, NoSize = 0x0001, NoZOrder = 0x0004;
    private readonly MainViewModel _viewModel;
    private readonly WindowDisplays _displays = new();
    private bool _closing, _closeComplete;
    private readonly DispatcherTimer _controlsTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _mediaTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _osdTimer = new() { Interval = TimeSpan.FromMilliseconds(1800) };
    private readonly DispatcherTimer _videoClickTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private bool _readingMedia;
    private DateTime _lastInteraction = DateTime.UtcNow;
    private ScreenPoint _lastCursor;
    private bool _controlsHovered;
    public IReadOnlyList<DisplayChoice> Displays { get; } = WindowDisplays.Enumerate();
    public DisplayChoice? SelectedDisplay { get; set; }
    public ChannelLogoLoader LogoLoader { get; }

    public MainWindow(AppServices services) : this(
        new MainViewModel(
            services.ProviderFactory,
            services.Playback,
            services.Profiles,
            services.Logger,
            services.Catalogs,
            services.Credentials,
            services.Viewer,
            services.Content,
            new StreamDesk.Probe.Infrastructure.XmlTvReader(),
            services.Recovery,
            services.Playlists,
            services.Settings,
            services.Sync,
            services.ParentalControl),
        services.Logos)
    { }

    public MainWindow(MainViewModel viewModel, ChannelLogoLoader logoLoader)
    {
        _viewModel = viewModel;
        LogoLoader = logoLoader;
        SelectedDisplay = Displays.FirstOrDefault();
        InitializeComponent();
        DataContext = _viewModel;
        SourceInitialized += (_, _) => EnsureNativeCaptionButtons();
        Loaded += async (_, _) =>
        {
            await _viewModel.InitializePlaybackAsync();
            await _viewModel.InitializeSettingsAsync();
            if (!_closing) await _viewModel.LoadProfilesAsync();
        };
        _viewModel.ThemeChanged += ApplyTheme;
        _viewModel.UiScaleChanged += ApplyUiScale;
        _viewModel.PropertyChanged += ViewModelChanged;
        SystemEvents.PowerModeChanged += PowerModeChanged;
        Closing += WindowClosing;
        PreviewKeyDown += WindowKeyDown;
        SizeChanged += (_, _) => UpdateLayoutMode();
        _controlsTimer.Tick += (_, _) => UpdateControls();
        _controlsTimer.Start();
        _osdTimer.Tick += (_, _) =>
        {
            _osdTimer.Stop();
        _videoClickTimer.Stop();
            _viewModel.ShowOsd = false;
        };
        _mediaTimer.Tick += async (_, _) =>
        {
            if (_readingMedia || _closing || AudioTrackBox.IsKeyboardFocusWithin || SubtitleTrackBox.IsKeyboardFocusWithin || SeekSlider.IsMouseCaptureWithin) return;
            _readingMedia = true;
            try { await _viewModel.RefreshPlaybackDetailsAsync(); }
            finally { _readingMedia = false; }
        };
        _videoClickTimer.Tick += async (_, _) =>
        {
            _videoClickTimer.Stop();
            if (_viewModel.CanStopPlayback) await _viewModel.TogglePlayPauseAsync();
        };
        _mediaTimer.Start();
        UpdateLayoutMode();
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        PasswordBox.Clear();
        await _viewModel.ConnectAsync(BaseUrlBox.Text, UsernameBox.Text, password);
        _viewModel.CurrentTabIndex = 1;
    }

    private async void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        var password = ProfilePasswordBox.Password;
        ProfilePasswordBox.Clear();
        await _viewModel.CreateProfileAsync(password);
    }

    private async void SaveAndWatch_Click(object sender, RoutedEventArgs e)
    {
        var password = ProfilePasswordBox.Password;
        ProfilePasswordBox.Clear();
        if (!await _viewModel.CreateProfileAsync(password)) return;
        _viewModel.CurrentTabIndex = 1;
        await _viewModel.LoadCatalogAsync();
    }

    private async void UseSelectedProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProfile is null) return;
        _viewModel.ActiveProfile = _viewModel.SelectedProfile;
        _viewModel.CurrentTabIndex = 1;
        if (_viewModel.Channels.Count == 0 && _viewModel.CanConnect) await _viewModel.LoadCatalogAsync();
    }

    private async void PlaySelected_Click(object sender, RoutedEventArgs e) => await _viewModel.PlaySelectedAsync();
    private async void Retry_Click(object sender, RoutedEventArgs e) => await _viewModel.RetryPlaybackAsync();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await _viewModel.StopPlaybackAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _viewModel.NavigateChannelAsync(-1);
    private async void Next_Click(object sender, RoutedEventArgs e) => await _viewModel.NavigateChannelAsync(1);
    private async void TogglePlay_Click(object sender, RoutedEventArgs e) => await _viewModel.TogglePlaybackAsync();
    private async void TogglePlayPause_Click(object sender, RoutedEventArgs e) => await _viewModel.TogglePlayPauseAsync();
    private async void PlayLast_Click(object sender, RoutedEventArgs e) => await _viewModel.PlayLastAsync();
    private async void AutoPlay_Click(object sender, RoutedEventArgs e) => await _viewModel.SetAutoPlayAsync(AutoPlayCheckBox.IsChecked == true);
    private async void ProbeContent_Click(object sender, RoutedEventArgs e) => await _viewModel.ProbeContentAsync();
    private async void PlayMedia_Click(object sender, RoutedEventArgs e) => await _viewModel.PlayMediaAsync();
    private async void PlayRandomEpisode_Click(object sender, RoutedEventArgs e) => await _viewModel.PlayRandomEpisodeAsync();
    private void CycleAmplification_Click(object sender, RoutedEventArgs e) => _viewModel.CycleAmplification();
    private async void RestartMedia_Click(object sender, RoutedEventArgs e) => await _viewModel.PlayMediaAsync(true);
    private async void RefreshEpg_Click(object sender, RoutedEventArgs e) => await _viewModel.RefreshEpgAsync(true);
    private async void ApplyAudio_Click(object sender, RoutedEventArgs e) => await _viewModel.SelectTrackAsync(false);
    private async void Backup_Click(object sender, RoutedEventArgs e) => await _viewModel.CreateBackupAsync();
    private async void Diagnostics_Click(object sender, RoutedEventArgs e) => await _viewModel.CreateBackupAsync(true);
    private async void ApplySubtitle_Click(object sender, RoutedEventArgs e) => await _viewModel.SelectTrackAsync(true);
    private async void AudioTrack_Changed(object sender, EventArgs e) => await _viewModel.SelectTrackAsync(false);
    private async void SubtitleTrack_Changed(object sender, EventArgs e) => await _viewModel.SelectTrackAsync(true);
    private async void Seek_Completed(object sender, MouseButtonEventArgs e) => await _viewModel.SeekToAsync(SeekSlider.Value);
    private async void Seek_KeyUp(object sender, KeyEventArgs e) { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) await _viewModel.SeekToAsync(SeekSlider.Value); }
    private async void SeekBackward_Click(object sender, RoutedEventArgs e) => await _viewModel.SeekRelativeAsync(-10);
    private async void SeekForward_Click(object sender, RoutedEventArgs e) => await _viewModel.SeekRelativeAsync(10);
    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Any(file => file.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }
    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        var playlist = files.FirstOrDefault(file =>
            file.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(playlist)) return;
        e.Handled = true;
        await _viewModel.ImportLocalPlaylistAsync(playlist);
        if (_viewModel.Channels.Count > 0) _viewModel.CurrentTabIndex = 1;
    }
    private async void ImportEpg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "XMLTV (*.xml)|*.xml", Title = "Cargar archivo de guía (.xml)" };
        if (dialog.ShowDialog(this) != true) return;
        try { await using var stream = File.OpenRead(dialog.FileName); await _viewModel.ImportEpgAsync(stream); }
        catch (Exception exception) { _viewModel.ReportWindowError(exception); }
    }
    private async void ImportPlaylistFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Listas M3U (*.m3u;*.m3u8)|*.m3u;*.m3u8", Title = "Importar lista M3U/M3U8" };
        if (dialog.ShowDialog(this) == true) await _viewModel.ImportLocalPlaylistAsync(dialog.FileName);
    }
    private async void ImportPlaylistUrl_Click(object sender, RoutedEventArgs e)
    {
        var source = RemotePlaylistBox.Text.Trim();
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == "http")
        {
            var choice = System.Windows.MessageBox.Show(this,
                "Esta lista usa HTTP sin cifrar. Los datos y tokens que incluya podrían ser visibles durante la descarga. Si tu proveedor ofrece HTTPS, úsalo. ¿Continuar de todos modos?",
                "Conexión sin cifrar", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (choice != System.Windows.MessageBoxResult.Yes) return;
        }
        await _viewModel.ImportRemotePlaylistAsync(source);
    }
    private async void CreateM3uProfile_Click(object sender, RoutedEventArgs e) => await _viewModel.CreateM3uProfileAsync();
    private async void ExportSync_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Paquete de sincronización StreamDesk (*.streamdesk-sync)|*.streamdesk-sync|Todos los archivos (*.*)|*.*",
            FileName = $"streamdesk-sync-{DateTime.Now:yyyyMMdd-HHmm}.streamdesk-sync",
            Title = "Exportar favoritos e historial cifrados"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SyncPassphrase = SyncPasswordBox.Password;
            await _viewModel.ExportSyncAsync(dialog.FileName);
            SyncPasswordBox.Clear();
        }
    }
    private async void ImportSync_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Paquete de sincronización StreamDesk (*.streamdesk-sync)|*.streamdesk-sync|Todos los archivos (*.*)|*.*",
            Title = "Importar favoritos e historial cifrados"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SyncPassphrase = SyncPasswordBox.Password;
            await _viewModel.ImportSyncAsync(dialog.FileName);
            SyncPasswordBox.Clear();
        }
    }
    private async void ConfigureParental_Click(object sender, RoutedEventArgs e)
    {
        var pin = ParentalPasswordBox.Password;
        if (await _viewModel.ConfigureParentalControlAsync(pin)) ParentalPasswordBox.Clear();
    }
    private void UnlockParental_Click(object sender, RoutedEventArgs e)
    {
        var pin = ParentalPasswordBox.Password;
        if (_viewModel.UnlockParental(pin)) ParentalPasswordBox.Clear();
    }
    private void LockParental_Click(object sender, RoutedEventArgs e) => _viewModel.LockParental();
    private async void DisableParental_Click(object sender, RoutedEventArgs e)
    {
        var pin = ParentalPasswordBox.Password;
        if (await _viewModel.DisableParentalControlAsync(pin)) ParentalPasswordBox.Clear();
    }
    private void ApplyTheme(string theme)
    {
        var isLight = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        Resources["CanvasBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(243, 243, 243) : Color.FromRgb(15, 17, 21));
        Resources["BackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(243, 243, 243) : Color.FromRgb(18, 18, 18));
        Resources["SurfaceBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(30, 30, 30));
        Resources["SurfaceRaisedBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(235, 235, 235) : Color.FromRgb(40, 40, 40));
        Resources["TextPrimaryBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(26, 26, 26) : Color.FromRgb(255, 255, 255));
        Resources["TextSecondaryBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(102, 102, 102) : Color.FromRgb(160, 160, 160));
        Resources["BorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(220, 220, 220) : Color.FromRgb(46, 46, 46));
        Resources["AccentBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(0, 120, 212) : Color.FromRgb(96, 205, 255));
    }
    private void ApplyUiScale(string scale)
    {
        var factor = scale switch { "115%" => 1.15, "130%" => 1.30, _ => 1.0 };
        if (Shell is not null) Shell.LayoutTransform = factor == 1.0 ? Transform.Identity : new ScaleTransform(factor, factor);
    }
    private void Navigate_Click(object sender, RoutedEventArgs e) => _viewModel.CurrentTabIndex = int.Parse((string)((FrameworkElement)sender).Tag);
    private void ClearFilters_Click(object sender, RoutedEventArgs e) => _viewModel.ClearFilters();
    private void ClearSearch_Click(object sender, RoutedEventArgs e) => _viewModel.SearchText = "";
    private void ClearMediaSearch_Click(object sender, RoutedEventArgs e) => _viewModel.MediaSearch = "";
    private void ClearCategorySearch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CategorySearch = "";
        if (CategoryFilter.Template?.FindName("CategoryFilterSearchBox", CategoryFilter) is System.Windows.Controls.TextBox searchBox)
        {
            searchBox.Focus();
        }
    }
    private void ClearMediaCategorySearch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.MediaCategorySearch = "";
        if (sender is FrameworkElement fe && fe.TemplatedParent is ComboBox combo &&
            combo.Template?.FindName("MediaCategoryFilterSearchBox", combo) is System.Windows.Controls.TextBox searchBox)
        {
            searchBox.Focus();
        }
    }
    private async void BackToMediaBrowse_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.StopPlaybackAsync();
        _viewModel.IsImmersiveMode = false;
        _viewModel.SelectedMedia = null;
    }
    private void ClearMediaFilters_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.MediaSearch = "";
        _viewModel.MediaCategorySearch = "";
        _viewModel.MediaCategory = null;
    }
    private void CategoryFilter_DropDownOpened(object sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            if (CategoryFilter.Template?.FindName("CategoryFilterSearchBox", CategoryFilter) is System.Windows.Controls.TextBox searchBox)
            {
                searchBox.Focus();
                searchBox.SelectAll();
            }
        });
    }
    private void MediaCategoryFilter_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is ComboBox combo)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                if (combo.Template?.FindName("MediaCategoryFilterSearchBox", combo) is System.Windows.Controls.TextBox searchBox)
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                }
            });
        }
    }
    private void CategorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox && !textBox.IsKeyboardFocused)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                var caret = textBox.CaretIndex;
                textBox.Focus();
                textBox.CaretIndex = caret;
            });
        }
    }
    private void MediaCategorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox && !textBox.IsKeyboardFocused)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                var caret = textBox.CaretIndex;
                textBox.Focus();
                textBox.CaretIndex = caret;
            });
        }
    }
    private void CategorySearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && CategoryFilter.Items.Count > 0)
        {
            CategoryFilter.SelectedIndex = 0;
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Escape)
        {
            CategoryFilter.IsDropDownOpen = false;
            e.Handled = true;
        }
    }
    private void MediaCategorySearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.TemplatedParent is ComboBox combo)
        {
            if (e.Key == Key.Down && combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key is Key.Enter or Key.Escape)
            {
                combo.IsDropDownOpen = false;
                e.Handled = true;
            }
        }
    }
    private async void FavoriteRow_Click(object sender, RoutedEventArgs e)
    { e.Handled = true; await _viewModel.ToggleFavoriteAsync((XtreamChannel)((FrameworkElement)sender).DataContext); }
    private async void Channels_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (HasInteractiveOrScrollAncestor(e.OriginalSource as DependencyObject) || FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: XtreamChannel channel }) return;
        _viewModel.SelectedChannel = channel;
        await _viewModel.PlaySelectedAsync();
    }
    private async void Channels_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SelectedChannel is not null) { e.Handled = true; await _viewModel.PlaySelectedAsync(); }
    }
    private async void Episodes_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (HasInteractiveOrScrollAncestor(e.OriginalSource as DependencyObject) || FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: MediaItem episode }) return;
        _viewModel.SelectedEpisode = episode;
        _viewModel.IsEpisodePlayerFocus = true;
        UpdateLayoutMode();
        await _viewModel.PlayMediaAsync();
    }
    private async void Episodes_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SelectedEpisode is not null)
        {
            e.Handled = true;
            _viewModel.IsEpisodePlayerFocus = true;
            UpdateLayoutMode();
            await _viewModel.PlayMediaAsync();
        }
    }
    private void BackToEpisodes_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ReturnFromImmersive();
        UpdateLayoutMode();
    }
    private async void ContinueWatching_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (HasInteractiveOrScrollAncestor(e.OriginalSource as DependencyObject) || FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: MediaProgress progress }) return;
        _viewModel.SelectedProgress = progress;
        await _viewModel.PlayMediaAsync();
    }
    private void VideoSurface_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RevealControls();
        if (e.ClickCount == 2)
        {
            _videoClickTimer.Stop();
            ToggleFullscreen_Click(sender, e);
        }
        else if (e.ClickCount == 1)
        {
            _videoClickTimer.Stop();
            _videoClickTimer.Start();
        }
    }
    private void VideoSurface_TouchDown(object? sender, TouchEventArgs e) => RevealControls();
    private void Controls_TouchDown(object? sender, TouchEventArgs e) => RevealControls();
    private void CancelCatalog_Click(object sender, RoutedEventArgs e) => _viewModel.CancelCatalog();

    private void VideoView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PlayerHandle is MediaPlayer mediaPlayer) VideoView.MediaPlayer = mediaPlayer;
    }

    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.ShowPlayerPane) return;
        try { _displays.Toggle(this, SelectedDisplay); UpdateLayoutMode(); RevealControls(); }
        catch (Exception exception) { UpdateLayoutMode(); _viewModel.ReportWindowError(exception); }
    }

    private void TogglePictureInPicture_Click(object sender, RoutedEventArgs e)
    {
        if (_closing || !_viewModel.ShowTv || !_viewModel.ShowPlayerPane) return;
        try { _displays.TogglePictureInPicture(this); UpdateLayoutMode(); RevealControls(); }
        catch (Exception exception) { UpdateLayoutMode(); _viewModel.ReportWindowError(exception); }
    }

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_closing) return;
        if (args.PropertyName == nameof(MainViewModel.PlayerHandle)) VideoView_Loaded(this, new RoutedEventArgs());
        if (args.PropertyName is nameof(MainViewModel.OsdText) or nameof(MainViewModel.ShowOsd) && _viewModel.ShowOsd)
        {
            _osdTimer.Stop();
        _videoClickTimer.Stop();
            _osdTimer.Start();
        }
        if (args.PropertyName == nameof(MainViewModel.CurrentTabIndex) && _displays.IsPictureInPicture && !_viewModel.ShowTv)
        {
            try { _displays.TogglePictureInPicture(this); UpdateLayoutMode(); }
            catch (Exception exception) { _viewModel.ReportWindowError(exception); }
        }
        if (args.PropertyName is nameof(MainViewModel.CurrentTabIndex) or nameof(MainViewModel.ShowMediaBrowse) or nameof(MainViewModel.ShowMovieDetails) or nameof(MainViewModel.ShowSeriesDetails) or nameof(MainViewModel.ShowPlayerPane) or nameof(MainViewModel.IsEpisodePlayerFocus) or nameof(MainViewModel.IsImmersiveMode)) UpdateLayoutMode();
        if (args.PropertyName is nameof(MainViewModel.CanHideControls) or nameof(MainViewModel.CurrentTabIndex)) RevealControls();
    }

    private async void WindowKeyDown(object sender, KeyEventArgs args)
    {
        RevealControls();
        if (args.Key == Key.Escape && _viewModel.IsImmersiveMode && !_displays.IsFullscreen && !_displays.IsPictureInPicture)
        {
            args.Handled = true;
            _viewModel.ReturnFromImmersive();
            UpdateLayoutMode();
            return;
        }
        if (args.Key is Key.Up or Key.Down && HasListAncestor(args.OriginalSource as DependencyObject)) return;
        var action = PlaybackShortcuts.Resolve(args.Key, Keyboard.Modifiers, HasEditingAncestor(args.OriginalSource as DependencyObject ?? Keyboard.FocusedElement as DependencyObject), _displays.IsFullscreen, _viewModel.ShowTv, _displays.IsPictureInPicture);
        if (action == PlaybackShortcut.None || args.IsRepeat) return;
        args.Handled = true;
        switch (action)
        {
            case PlaybackShortcut.Toggle: await _viewModel.TogglePlaybackAsync(); break;
            case PlaybackShortcut.Previous: await _viewModel.NavigateChannelAsync(-1); break;
            case PlaybackShortcut.Next: await _viewModel.NavigateChannelAsync(1); break;
            case PlaybackShortcut.SeekBackward: await _viewModel.SeekRelativeAsync(-10); break;
            case PlaybackShortcut.SeekForward: await _viewModel.SeekRelativeAsync(10); break;
            case PlaybackShortcut.VolumeUp: _viewModel.Volume += 5; break;
            case PlaybackShortcut.VolumeDown: _viewModel.Volume -= 5; break;
            case PlaybackShortcut.Mute: _viewModel.IsMuted = !_viewModel.IsMuted; break;
            case PlaybackShortcut.Fullscreen: case PlaybackShortcut.ExitFullscreen: ToggleFullscreen_Click(this, new RoutedEventArgs()); break;
            case PlaybackShortcut.PictureInPicture: case PlaybackShortcut.ExitPictureInPicture: TogglePictureInPicture_Click(this, new RoutedEventArgs()); break;
        }
    }
    private static bool HasEditingAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBoxBase or System.Windows.Controls.PasswordBox or ComboBox or Slider or ButtonBase) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
    private static bool HasInteractiveOrScrollAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or ScrollBar or Thumb) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }
    private static bool HasListAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBox) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
    private void UpdateLayoutMode()
    {
        if (Shell is null) return;
        var fullscreen = _displays.IsFullscreen;
        var picture = _displays.IsPictureInPicture;
        if (!fullscreen) EnsureNativeCaptionButtons();
        var compact = fullscreen || picture;
        var immersive = !compact && _viewModel.IsImmersiveMode;
        var hideChrome = compact || immersive;
        if (AppTitleBar is not null) AppTitleBar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        Shell.Margin = new Thickness(fullscreen ? 0 : picture ? 6 : 16);
        Sidebar.Visibility = TvHeader.Visibility = ConnectionNotices.Visibility = CatalogPane.Visibility = StatusBar.Visibility = hideChrome ? Visibility.Collapsed : Visibility.Visible;
        NowPlayingTitle.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = new GridLength(hideChrome ? 0 : ActualWidth < 1050 ? 132 : 158);
        var mediaDetails = (_viewModel.ShowMovieDetails || _viewModel.ShowSeriesDetails) && !immersive;
        CatalogColumn.Width = new GridLength(hideChrome ? 0 : mediaDetails ? ActualWidth < 1050 ? 260 : 360 : ActualWidth < 1050 ? 234 : 284);
        Workspace.Margin = new Thickness(hideChrome ? 0 : 20, 0, 0, 0);



        PipHeader.Visibility = PipControls.Visibility = picture ? Visibility.Visible : Visibility.Collapsed;
        StandardControls.Visibility = picture ? Visibility.Collapsed : Visibility.Visible;
        EpgPanel.Visibility = !hideChrome ? Visibility.Visible : Visibility.Collapsed;
        MediaBrowsePane.Visibility = !hideChrome && _viewModel.ShowMediaBrowse ? Visibility.Visible : Visibility.Collapsed;
        MovieDetailsPane.Visibility = !hideChrome && _viewModel.ShowMovieDetails ? Visibility.Visible : Visibility.Collapsed;
        SeriesDetailsPane.Visibility = !hideChrome && _viewModel.ShowSeriesDetails ? Visibility.Visible : Visibility.Collapsed;



        PlayerPane.Visibility = _viewModel.ShowPlayerPane ? Visibility.Visible : Visibility.Collapsed;
        PlaybackCaption.MaxHeight = picture ? 36 : double.PositiveInfinity;
        PlayerControls.Padding = new Thickness(picture ? 6 : 10);
    }
    private void EnsureNativeCaptionButtons()
    {
        if (_displays.IsFullscreen) return;
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        var style = GetWindowLong(handle, WindowStyleIndex);
        if ((style & SystemMenuStyle) != 0) return;
        SetWindowLong(handle, WindowStyleIndex, style | SystemMenuStyle);
        if (!SetWindowPos(handle, 0, 0, 0, 0, 0, FrameChanged | NoMove | NoSize | NoZOrder))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong(nint handle, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);
    private void RevealControls() { _lastInteraction = DateTime.UtcNow; PlayerControls.Visibility = Visibility.Visible; }
    private void Player_MouseMove(object sender, MouseEventArgs e) => RevealControls();
    private void Player_MouseLeave(object sender, MouseEventArgs e) { _controlsHovered = false; }
    private void Controls_MouseEnter(object sender, MouseEventArgs e) { _controlsHovered = true; RevealControls(); }
    private void Controls_MouseLeave(object sender, MouseEventArgs e) { _controlsHovered = false; RevealControls(); }
    private void UpdateControls()
    {
        // Poll the cursor because native video owns mouse messages inside its HWND.
        if (_viewModel.ShowTv && GetCursorPos(out var cursor))
        {
            if ((cursor.X != _lastCursor.X || cursor.Y != _lastCursor.Y) && PlayerPane.IsVisible)
            {
                var point = PlayerPane.PointFromScreen(new Point(cursor.X, cursor.Y));
                if (point.X >= 0 && point.Y >= 0 && point.X < PlayerPane.ActualWidth && point.Y < PlayerPane.ActualHeight) RevealControls();
            }
            _lastCursor = cursor;
        }
        if (_displays.IsPictureInPicture || !_viewModel.CanHideControls || _controlsHovered || PlayerControls.IsKeyboardFocusWithin) { PlayerControls.Visibility = Visibility.Visible; return; }
        if (DateTime.UtcNow - _lastInteraction > TimeSpan.FromSeconds(3)) PlayerControls.Visibility = Visibility.Collapsed;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out ScreenPoint point);

    private void PowerModeChanged(object sender, PowerModeChangedEventArgs args)
    { if (args.Mode == PowerModes.Suspend && !_closing) _ = _viewModel.SuspendPlaybackAsync(); }

    private async void WindowClosing(object? sender, CancelEventArgs args)
    {
        if (_closeComplete) return;
        args.Cancel = true;
        if (_closing) return;
        _closing = true;
        _controlsTimer.Stop();
        _mediaTimer.Stop();
        _osdTimer.Stop();
        _videoClickTimer.Stop();
        SystemEvents.PowerModeChanged -= PowerModeChanged;
        _viewModel.PropertyChanged -= ViewModelChanged;
        VideoView.MediaPlayer = null;
        await _viewModel.DisposeAsync();
        _closeComplete = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    protected override void OnClosed(EventArgs e)
    {
        VideoView.MediaPlayer = null;
        VideoView.Dispose();
        base.OnClosed(e);
    }
}
