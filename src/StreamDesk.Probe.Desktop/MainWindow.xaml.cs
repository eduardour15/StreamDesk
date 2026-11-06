using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Data;
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
    private readonly DispatcherTimer _controlsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _mediaTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _osdTimer = new() { Interval = TimeSpan.FromMilliseconds(1800) };
    private readonly DispatcherTimer _videoClickTimer = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private bool _readingMedia;
    private DateTime _lastInteraction = DateTime.UtcNow;
    private ScreenPoint _lastCursor;
    private bool _controlsHovered;
    private bool _keyboardControls;
    private Window? _overlayWindow;
    private DispatcherOperation? _categoryFocusOperation;
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
        VideoView.IsVisibleChanged += (_, _) => UpdatePlayerOverlayVisibility();
        // VideoView moves this content to its own foreground window, outside this visual tree.
        PlayerOverlay.Resources.MergedDictionaries.Add(Resources);
        PipTopmost.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new Binding(nameof(Topmost)) { Source = this, Mode = BindingMode.TwoWay });
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
        StateChanged += (_, _) => { RevealControls(); UpdateTimers(); UpdatePlayerOverlayVisibility(); };
        Deactivated += PlayerWindow_Deactivated;
        Activated += (_, _) => RevealControls();
        _controlsTimer.Tick += (_, _) => UpdateControls();
        _osdTimer.Tick += (_, _) =>
        {
            _osdTimer.Stop();
            _viewModel.ShowOsd = false;
        };
        _mediaTimer.Tick += async (_, _) =>
        {
            if (_readingMedia || _closing || AudioTrackBox.IsDropDownOpen || SubtitleTrackBox.IsDropDownOpen || FormatBox.IsDropDownOpen || FormatBox.IsKeyboardFocusWithin ||
                AudioTrackBox.IsKeyboardFocusWithin || SubtitleTrackBox.IsKeyboardFocusWithin || SeekSlider.IsMouseCaptureWithin ||
                SeekSlider.AreAnyTouchesCapturedWithin || (SeekSlider.IsKeyboardFocusWithin && (Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right)))) return;
            _readingMedia = true;
            try { await _viewModel.RefreshPlaybackDetailsAsync(true); }
            finally { _readingMedia = false; }
        };
        _videoClickTimer.Tick += async (_, _) =>
        {
            _videoClickTimer.Stop();
            if (_viewModel.CanStopPlayback) await _viewModel.TogglePlayPauseAsync();
        };
        UpdateTimers();
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
    private async void ApplyLivePreferences_Click(object sender, RoutedEventArgs e) => await _viewModel.ApplyLivePreferencesAsync();
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
    private void ToggleAudioSubtitles_Click(object sender, RoutedEventArgs e) => _viewModel.ToggleAudioSubtitles();
    private void CloseAudioSubtitles_Click(object sender, RoutedEventArgs e) => _viewModel.CloseAudioSubtitles();
    private void TogglePlaybackStats_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePlaybackStats();
    private void ClosePlaybackStats_Click(object sender, RoutedEventArgs e) => _viewModel.ClosePlaybackStats();
    private void ToggleEpisodesDrawer_Click(object sender, RoutedEventArgs e) => _viewModel.ToggleEpisodesDrawer();
    private void CloseEpisodesDrawer_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseEpisodesDrawer();
        if (EpisodesDrawerButton.IsVisible) EpisodesDrawerButton.Focus();
    }
    private void SwitchToSeasonsView_Click(object sender, RoutedEventArgs e) => _viewModel.SwitchToSeasonsView();
    private void SwitchToEpisodesView_Click(object sender, RoutedEventArgs e) => _viewModel.SwitchToEpisodesView();
    private void SeasonItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DrawerSeasonItem seasonItem })
            _viewModel.SelectDrawerSeason(seasonItem.Category);
    }
    private async void DrawerEpisodeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DrawerEpisodeItem epItem })
            await _viewModel.PlayEpisodeFromDrawerAsync(epItem.Episode);
    }
    private async void AudioTrackItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaTrackInfo track })
            await _viewModel.SelectAudioTrackInfoAsync(track);
    }
    private async void SubtitleTrackItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaTrackInfo track })
            await _viewModel.SelectSubtitleTrackInfoAsync(track);
    }
    private async void PlayNextEpisodeNow_Click(object sender, RoutedEventArgs e) => await _viewModel.PlayNextEpisodeNowAsync();
    private void CancelNextEpisodeAutoPlay_Click(object sender, RoutedEventArgs e) => _viewModel.CancelNextEpisodeAutoPlay();
    private void AudioSubtitlesOverlay_MouseDown(object sender, MouseButtonEventArgs e) { }
    private void NextEpisodeOverlay_MouseDown(object sender, MouseButtonEventArgs e) { }
    private async void RefreshEpg_Click(object sender, RoutedEventArgs e) => await _viewModel.RefreshEpgAsync(true);
    private async void ApplyAudio_Click(object sender, RoutedEventArgs e) => await _viewModel.SelectTrackAsync(false);
    private async void Backup_Click(object sender, RoutedEventArgs e) => await _viewModel.CreateBackupAsync();
    private async void Diagnostics_Click(object sender, RoutedEventArgs e) => await _viewModel.CreateBackupAsync(true);
    private async void ApplySubtitle_Click(object sender, RoutedEventArgs e) => await _viewModel.SelectTrackAsync(true);
    private async void AudioTrack_Changed(object sender, EventArgs e) => await _viewModel.SelectTrackAsync(false);
    private async void SubtitleTrack_Changed(object sender, EventArgs e) => await _viewModel.SelectTrackAsync(true);
    private async void Format_Changed(object sender, EventArgs e) => await _viewModel.ApplyLiveFormatChangeAsync();
    private async void CycleStability_Click(object sender, RoutedEventArgs e) => await _viewModel.CycleStabilityPresetAsync();
    private void Seek_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _keyboardControls = false;
        RevealControls();
    }
    private async void Seek_Completed(object sender, MouseButtonEventArgs e)
    {
        SeekPreviewBadge.Visibility = Visibility.Collapsed;
        await _viewModel.SeekToAsync(SeekSlider.Value);
    }
    private async void Seek_KeyUp(object sender, KeyEventArgs e) { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) await _viewModel.SeekToAsync(SeekSlider.Value); }
    private void SeekSlider_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_viewModel.CanSeek || _viewModel.DurationSeconds <= 0 || SeekSlider.ActualWidth <= 0)
        {
            SeekPreviewBadge.Visibility = Visibility.Collapsed;
            return;
        }
        var pos = e.GetPosition(SeekSlider);
        var ratio = Math.Clamp(pos.X / SeekSlider.ActualWidth, 0.0, 1.0);
        var targetSeconds = ratio * _viewModel.DurationSeconds;
        var time = TimeSpan.FromSeconds(targetSeconds);
        SeekPreviewText.Text = time.Hours > 0 ? time.ToString(@"hh\:mm\:ss") : time.ToString(@"mm\:ss");

        SeekPreviewBadge.Visibility = Visibility.Visible;
        var badgeWidth = SeekPreviewBadge.ActualWidth > 0 ? SeekPreviewBadge.ActualWidth : 56;
        var left = Math.Clamp(pos.X - (badgeWidth / 2.0), 0, Math.Max(0, SeekSlider.ActualWidth - badgeWidth));
        Canvas.SetLeft(SeekPreviewBadge, left);
    }
    private void SeekSlider_MouseLeave(object sender, MouseEventArgs e)
    {
        SeekPreviewBadge.Visibility = Visibility.Collapsed;
    }
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
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(isLight ? Wpf.Ui.Appearance.ApplicationTheme.Light : Wpf.Ui.Appearance.ApplicationTheme.Dark,
            WindowBackdropType.None, false);
        Resources["CanvasBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(243, 243, 243) : Color.FromRgb(15, 17, 21));
        Resources["BackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(243, 243, 243) : Color.FromRgb(18, 18, 18));
        Resources["SurfaceBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(30, 30, 30));
        Resources["SurfaceRaisedBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(235, 235, 235) : Color.FromRgb(40, 40, 40));
        Resources["TextPrimaryBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(26, 26, 26) : Color.FromRgb(255, 255, 255));
        Resources["TextMutedBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(80, 88, 100) : Color.FromRgb(185, 196, 208));
        Resources["ControlBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(120, 128, 140) : Color.FromRgb(98, 107, 120));
        Resources["HoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(224, 231, 238) : Color.FromRgb(52, 60, 72));
        Resources["PressedBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(205, 221, 232) : Color.FromRgb(64, 82, 71));
        Resources["SelectionBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(213, 232, 247) : Color.FromRgb(38, 59, 53));
        Resources["TextSecondaryBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(102, 102, 102) : Color.FromRgb(160, 160, 160));
        Resources["BorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(220, 220, 220) : Color.FromRgb(46, 46, 46));
        Resources["AccentBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(0, 120, 212) : Color.FromRgb(96, 205, 255));
        // FluentWindow also changes its local background on theme updates.
        SetResourceReference(BackgroundProperty, "CanvasBrush");
        AppTitleBar.ApplicationTheme = isLight ? Wpf.Ui.Appearance.ApplicationTheme.Light : Wpf.Ui.Appearance.ApplicationTheme.Dark;
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
        if (sender is System.Windows.Controls.TextBox textBox) RestoreCategoryFocus(CategoryFilter, textBox);
    }
    private void MediaCategorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox) RestoreCategoryFocus(MediaCategoryFilter, textBox);
    }
    private void RestoreCategoryFocus(ComboBox combo, System.Windows.Controls.TextBox textBox)
    {
        if (textBox.IsKeyboardFocused || !combo.IsDropDownOpen) return;
        _categoryFocusOperation?.Abort();
        var caret = textBox.CaretIndex;
        _categoryFocusOperation = Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_closing && combo.IsDropDownOpen && textBox.IsVisible)
            {
                textBox.Focus();
                textBox.CaretIndex = Math.Min(caret, textBox.Text.Length);
            }
        });
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
        if (sender is FrameworkElement)
        {
            var combo = MediaCategoryFilter;
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
    private void Media_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (HasInteractiveOrScrollAncestor(e.OriginalSource as DependencyObject) || FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: MediaItem media }) return;
        _viewModel.SelectedMedia = media;
    }
    private async void Media_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HasInteractiveOrScrollAncestor(e.OriginalSource as DependencyObject) || FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: MediaItem media }) return;
        _viewModel.SelectedMedia = media;
        if (media.Kind == ContentKind.Movie)
        {
            await _viewModel.PlayMediaAsync();
        }
    }
    private async void Media_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SelectedMedia is not null)
        {
            e.Handled = true;
            if (_viewModel.SelectedMedia.Kind == ContentKind.Movie)
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
    private async void RemoveContinueWatching_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MediaProgress progress })
        {
            await _viewModel.RemoveFromContinueWatchingAsync(progress);
        }
    }
    private void VideoSurface_MouseDown(object sender, MouseButtonEventArgs e)
    {
        RevealControls();
        if (e.ClickCount == 2)
        {
            _videoClickTimer.Stop();
            if (_displays.IsPictureInPicture) TogglePictureInPicture_Click(sender, e);
            else ToggleFullscreen_Click(sender, e);
        }
        else if (e.ClickCount == 1)
        {
            _videoClickTimer.Stop();
            if (_displays.IsPictureInPicture && e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
                return;
            }
            _videoClickTimer.Start();
        }
    }

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _keyboardControls = false;
        var source = e.OriginalSource as DependencyObject;
        if (_viewModel.IsEpisodesDrawerOpen)
        {
            var isInsideDrawer = IsDescendantOf(source, EpisodesDrawerOverlay);
            var isInsideDrawerButton = IsDescendantOf(source, EpisodesDrawerButton);
            if (!isInsideDrawer && !isInsideDrawerButton)
            {
                _viewModel.CloseEpisodesDrawer();
                e.Handled = true;
                return;
            }
        }
        if (_viewModel.IsAudioSubtitlesOpen)
        {
            var isInsidePanel = IsDescendantOf(source, AudioSubtitlesOverlay);
            var isInsideButton = IsDescendantOf(source, AudioSubtitlesButton);
            if (!isInsidePanel && !isInsideButton)
            {
                _viewModel.CloseAudioSubtitles();
                e.Handled = true;
                return;
            }
        }
        if (_viewModel.IsPlaybackStatsOpen)
        {
            var isInsideStats = IsDescendantOf(source, PlaybackStatsOverlay);
            var isInsideStatsButton = IsDescendantOf(source, PlaybackStatsButton);
            if (!isInsideStats && !isInsideStatsButton)
            {
                _viewModel.ClosePlaybackStats();
                e.Handled = true;
                return;
            }
        }
        RevealControls();
        if (IsDescendantOf(source, PlayerChrome) ||
            IsDescendantOf(source, EpisodesDrawerOverlay) ||
            IsDescendantOf(source, AudioSubtitlesOverlay) ||
            IsDescendantOf(source, PlaybackStatsOverlay) ||
            IsDescendantOf(source, NextEpisodeOverlay) ||
            IsDescendantOf(source, PipHeader) ||
            FindAncestor<ButtonBase>(source) is not null ||
            HasEditingAncestor(source)) return;
        PlayerOverlay.Focus();
        if (e.ChangedButton == MouseButton.Left)
        {
            VideoSurface_MouseDown(sender, e);
            e.Handled = true;
        }
    }
    private void PipHeader_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && HasInteractiveOrScrollAncestor(source)) return;
        if (e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
    }

    private void VideoSurface_TouchDown(object? sender, TouchEventArgs e) { _keyboardControls = false; RevealControls(); }
    private void Controls_TouchDown(object? sender, TouchEventArgs e) { _keyboardControls = false; RevealControls(); }
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
        try
        {
            _displays.TogglePictureInPicture(this);
            UpdateLayoutMode();
            RevealControls();
        }
        catch (Exception exception)
        {
            _viewModel.ReportWindowError(exception);
            try { UpdateLayoutMode(); } catch { }
        }
    }

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_closing) return;
        if (args.PropertyName == nameof(MainViewModel.PlayerHandle)) VideoView_Loaded(this, new RoutedEventArgs());
        if (args.PropertyName is nameof(MainViewModel.OsdText) or nameof(MainViewModel.ShowOsd) && _viewModel.ShowOsd)
        {
            _osdTimer.Stop();
            _osdTimer.Start();
        }
        if (args.PropertyName == nameof(MainViewModel.CurrentTabIndex) && _displays.IsPictureInPicture && !_viewModel.ShowTv)
        {
            try { _displays.TogglePictureInPicture(this); UpdateLayoutMode(); }
            catch (Exception exception) { _viewModel.ReportWindowError(exception); }
        }
        if (args.PropertyName is nameof(MainViewModel.CurrentTabIndex) or nameof(MainViewModel.ShowMediaBrowse) or nameof(MainViewModel.ShowMovieDetails) or nameof(MainViewModel.ShowSeriesDetails) or nameof(MainViewModel.ShowPlayerPane) or nameof(MainViewModel.IsEpisodePlayerFocus) or nameof(MainViewModel.IsImmersiveMode)) UpdateLayoutMode();
        if (args.PropertyName is nameof(MainViewModel.CanHideControls) or nameof(MainViewModel.CurrentTabIndex)) { RevealControls(); UpdateTimers(); }
    }

    private async void WindowKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Handled || !IsEnabled || !_viewModel.ShowTv || !_viewModel.ShowPlayerPane) return;
        var source = args.OriginalSource as DependencyObject ?? Keyboard.FocusedElement as DependencyObject;
        // Popups and modal dialogs keep their own Escape/navigation semantics.
        if (AudioTrackBox.IsDropDownOpen || SubtitleTrackBox.IsDropDownOpen || FormatBox.IsDropDownOpen ||
            (source is not null && Window.GetWindow(source) is { } sourceWindow && sourceWindow != this && sourceWindow != _overlayWindow)) return;
        _keyboardControls = args.Key == Key.Tab || PlayerChrome.IsKeyboardFocusWithin || PipHeader.IsKeyboardFocusWithin;
        if (args.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (_viewModel.IsEpisodesDrawerOpen)
            {
                _viewModel.CloseEpisodesDrawer();
                if (EpisodesDrawerButton.IsVisible) EpisodesDrawerButton.Focus();
                args.Handled = true;
                return;
            }
            if (_viewModel.IsAudioSubtitlesOpen)
            {
                _viewModel.CloseAudioSubtitles();
                args.Handled = true;
                return;
            }
            if (_viewModel.IsPlaybackStatsOpen)
            {
                _viewModel.ClosePlaybackStats();
                args.Handled = true;
                return;
            }
            if (_viewModel.IsNextEpisodeOverlayOpen)
            {
                _viewModel.CancelNextEpisodeAutoPlay();
                args.Handled = true;
                return;
            }
        }
        if (args.Key == Key.E && Keyboard.Modifiers == ModifierKeys.None && !HasEditingAncestor(source) && !HasListAncestor(source) && _viewModel.CanBrowseEpisodes)
        {
            _viewModel.ToggleEpisodesDrawer();
            args.Handled = true;
            return;
        }
        if ((args.Key is Key.S or Key.C) && Keyboard.Modifiers == ModifierKeys.None && !HasEditingAncestor(source) && !HasListAncestor(source))
        {
            _viewModel.ToggleAudioSubtitles();
            args.Handled = true;
            return;
        }
        RevealControls();
        if (args.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && _viewModel.IsImmersiveMode && !_displays.IsFullscreen && !_displays.IsPictureInPicture)
        {
            args.Handled = true;
            if (args.IsRepeat) return;
            _viewModel.ReturnFromImmersive();
            UpdateLayoutMode();
            return;
        }
        if (args.Key != Key.Escape && HasListAncestor(source)) return;
        var editing = HasEditingAncestor(source);
        if (FindAncestor<ButtonBase>(source) is not null && args.Key is not (Key.Space or Key.Enter)) editing = false;
        var action = PlaybackShortcuts.Resolve(args.Key, Keyboard.Modifiers, editing, _displays.IsFullscreen, _viewModel.ShowTv, _displays.IsPictureInPicture);
        if (action == PlaybackShortcut.None) return;
        if (action == PlaybackShortcut.Toggle && !_viewModel.CanStopPlayback) return;
        args.Handled = true;
        if (args.IsRepeat) return;
        switch (action)
        {
            case PlaybackShortcut.Toggle: await _viewModel.TogglePlaybackAsync(); break;
            case PlaybackShortcut.Previous: await _viewModel.NavigateChannelAsync(-1); break;
            case PlaybackShortcut.Next: await _viewModel.NavigateChannelAsync(1); break;
            case PlaybackShortcut.SeekBackward: await _viewModel.SeekRelativeAsync(-10); break;
            case PlaybackShortcut.SeekForward: await _viewModel.SeekRelativeAsync(10); break;
            case PlaybackShortcut.SeekShortBackward: await _viewModel.SeekRelativeAsync(-5); break;
            case PlaybackShortcut.SeekShortForward: await _viewModel.SeekRelativeAsync(5); break;
            case PlaybackShortcut.VolumeUp: _viewModel.Volume = Math.Clamp(_viewModel.Volume + 5, 0, 100); break;
            case PlaybackShortcut.VolumeDown: _viewModel.Volume = Math.Clamp(_viewModel.Volume - 5, 0, 100); break;
            case PlaybackShortcut.Mute: _viewModel.IsMuted = !_viewModel.IsMuted; break;
            case PlaybackShortcut.Fullscreen: case PlaybackShortcut.ExitFullscreen: ToggleFullscreen_Click(this, new RoutedEventArgs()); break;
            case PlaybackShortcut.PictureInPicture: case PlaybackShortcut.ExitPictureInPicture: TogglePictureInPicture_Click(this, new RoutedEventArgs()); break;
            case PlaybackShortcut.CycleBooster: _viewModel.CycleAmplification(); break;
        }
    }
    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject? source)
    {
        if (source is null) return null;
        DependencyObject? parent = null;
        if (source is Visual visual) parent = VisualTreeHelper.GetParent(visual);
        if (parent is null)
        {
            if (source is FrameworkElement fe && fe.Parent is not null) parent = fe.Parent;
            else parent = LogicalTreeHelper.GetParent(source);
            if (parent is null && source is Popup popup) parent = popup.PlacementTarget;
        }
        return parent;
    }
    private static bool IsDescendantOf(DependencyObject? source, DependencyObject? target)
    {
        if (source is null || target is null) return false;
        while (source is not null)
        {
            if (ReferenceEquals(source, target)) return true;
            source = GetVisualOrLogicalParent(source);
        }
        return false;
    }
    private static bool HasEditingAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBoxBase or System.Windows.Controls.PasswordBox or ComboBox or ComboBoxItem or Slider or ButtonBase or ScrollBar or Thumb) return true;
            if (source is Popup || source.GetType().Name == "PopupRoot") return true;
            source = GetVisualOrLogicalParent(source);
        }
        return false;
    }
    private static bool HasInteractiveOrScrollAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or ScrollBar or Thumb or ComboBox or ComboBoxItem) return true;
            if (source is Popup || source.GetType().Name == "PopupRoot") return true;
            source = GetVisualOrLogicalParent(source);
        }
        return false;
    }
    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = GetVisualOrLogicalParent(source);
        }
        return null;
    }
    private static bool HasListAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBox) return true;
            source = GetVisualOrLogicalParent(source);
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
        Shell.Margin = new Thickness(fullscreen ? 0 : picture ? 0 : 16);
        Sidebar.Visibility = TvHeader.Visibility = ConnectionNotices.Visibility = CatalogPane.Visibility = StatusBar.Visibility = hideChrome ? Visibility.Collapsed : Visibility.Visible;
        NowPlayingTitle.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = new GridLength(hideChrome ? 0 : ActualWidth < 1050 ? 150 : 176);
        var mediaDetails = (_viewModel.ShowMovieDetails || _viewModel.ShowSeriesDetails) && !immersive;
        var fullContinue = _viewModel.ShowContinueWatching && !_viewModel.ShowPlayerPane;
        CatalogColumn.Width = new GridLength(hideChrome ? 0 : fullContinue ? 1 : mediaDetails ? ActualWidth < 1050 ? 260 : 360 : ActualWidth < 1050 ? 234 : 284, fullContinue ? GridUnitType.Star : GridUnitType.Pixel);
        Workspace.Margin = new Thickness(hideChrome ? 0 : 20, 0, 0, 0);



        PipHeader.Visibility = PipControls.Visibility = picture ? Visibility.Visible : Visibility.Collapsed;
        StandardControls.Visibility = picture ? Visibility.Collapsed : Visibility.Visible;
        EpgPanel.Visibility = !hideChrome ? Visibility.Visible : Visibility.Collapsed;
        MediaBrowsePane.Visibility = !hideChrome && _viewModel.ShowMediaBrowse ? Visibility.Visible : Visibility.Collapsed;
        MovieDetailsPane.Visibility = !hideChrome && _viewModel.ShowMovieDetails ? Visibility.Visible : Visibility.Collapsed;
        SeriesDetailsPane.Visibility = !hideChrome && _viewModel.ShowSeriesDetails ? Visibility.Visible : Visibility.Collapsed;



        PlayerPane.Visibility = _viewModel.ShowTv && _viewModel.ShowPlayerPane ? Visibility.Visible : Visibility.Collapsed;
        PlaybackCaption.MaxHeight = picture ? 36 : double.PositiveInfinity;
        PlayerControls.Padding = new Thickness(picture ? 4 : 10, picture ? 2 : 6, picture ? 4 : 10, picture ? 2 : 6);
        PlayerControls.Margin = new Thickness(0, 4, 0, 0);
        PlayerChrome.Margin = new Thickness(picture ? 6 : 12);
        UpdatePlayerOverlayVisibility();
        UpdateTimers();
    }
    private void EnsureNativeCaptionButtons()
    {
        if (_displays.IsFullscreen) return;
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        try
        {
            var style = GetWindowLong(handle, WindowStyleIndex);
            if ((style & SystemMenuStyle) != 0) return;
            SetWindowLong(handle, WindowStyleIndex, style | SystemMenuStyle);
            SetWindowPos(handle, 0, 0, 0, 0, 0, FrameChanged | NoMove | NoSize | NoZOrder);
        }
        catch (Exception exception)
        {
            _viewModel.ReportWindowError(exception);
        }
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong(nint handle, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);
    private void PlayerOverlay_Loaded(object sender, RoutedEventArgs e)
    {
        var overlay = Window.GetWindow(PlayerOverlay);
        if (overlay == _overlayWindow) { UpdatePlayerOverlayVisibility(); return; }
        if (_overlayWindow is not null)
        {
            _overlayWindow.Deactivated -= PlayerWindow_Deactivated;
            _overlayWindow.Activated -= PlayerWindow_Activated;
        }
        _overlayWindow = overlay;
        if (_overlayWindow is not null)
        {
            _overlayWindow.Deactivated += PlayerWindow_Deactivated;
            _overlayWindow.Activated += PlayerWindow_Activated;
        }
        UpdatePlayerOverlayVisibility();
    }

    private void UpdatePlayerOverlayVisibility()
    {
        if (_overlayWindow is null || _closing) return;
        // The foreground is a separate top-level HWND: Collapsed on TvPage doesn't hide it.
        var visible = _viewModel.ShowTv && _viewModel.ShowPlayerPane && VideoView.IsVisible && WindowState != WindowState.Minimized;
        if (!visible)
        {
            PlayerOverlay.Cursor = Cursors.Arrow;
            _videoClickTimer.Stop();
            _controlsHovered = _keyboardControls = false;
            if (_overlayWindow.IsVisible) _overlayWindow.Hide();
        }
        else if (!_overlayWindow.IsVisible)
        {
            var showActivated = _overlayWindow.ShowActivated;
            _overlayWindow.ShowActivated = false;
            try { _overlayWindow.Show(); }
            finally { _overlayWindow.ShowActivated = showActivated; }
        }
    }

    private void PlayerWindow_Activated(object? sender, EventArgs e) => RevealControls();

    private void PlayerWindow_Deactivated(object? sender, EventArgs e)
    {
        // Focus can move between the owner and LibVLCSharp's foreground window.
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsActive && _overlayWindow?.IsActive != true)
            {
                _videoClickTimer.Stop();
                RevealControls();
            }
        }));
    }

    private void RevealControls()
    {
        _lastInteraction = DateTime.UtcNow;
        PlayerOverlay.Cursor = Cursors.Arrow;
        PlayerChrome.Visibility = PlayerControls.Visibility = Visibility.Visible;
        if (_displays.IsPictureInPicture) PipHeader.Visibility = Visibility.Visible;
    }
    private void Player_MouseMove(object sender, MouseEventArgs e) => RevealControls();
    private void Player_MouseLeave(object sender, MouseEventArgs e)
    {
        _controlsHovered = false;
        PlayerOverlay.Cursor = Cursors.Arrow;
    }
    private void Controls_MouseEnter(object sender, MouseEventArgs e) { _controlsHovered = true; RevealControls(); }
    private void Controls_MouseLeave(object sender, MouseEventArgs e) { _controlsHovered = false; RevealControls(); }
    private void UpdateControls()
    {
        // Compare physical coordinates as well as overlay events, including when leaving its HWND.
        var isCursorInsidePlayer = false;
        if (_viewModel.ShowTv && GetCursorPos(out var cursor))
        {
            if (PlayerPane.IsVisible)
            {
                try
                {
                    var point = PlayerOverlay.PointFromScreen(new Point(cursor.X, cursor.Y));
                    isCursorInsidePlayer = point.X >= 0 && point.Y >= 0 && point.X < PlayerOverlay.ActualWidth && point.Y < PlayerOverlay.ActualHeight;
                }
                catch { }
            }
            if (isCursorInsidePlayer && (cursor.X != _lastCursor.X || cursor.Y != _lastCursor.Y))
            { 
                RevealControls();
            }
            _lastCursor = cursor;
        }
        var interacting = _controlsHovered || PlayerChrome.IsMouseOver || PipHeader.IsMouseOver ||
            (_keyboardControls && (PlayerChrome.IsKeyboardFocusWithin || PipHeader.IsKeyboardFocusWithin)) ||
            PlayerOverlay.IsMouseCaptureWithin || PlayerOverlay.AreAnyTouchesCapturedWithin ||
            AudioTrackBox.IsDropDownOpen || SubtitleTrackBox.IsDropDownOpen || FormatBox.IsDropDownOpen ||
            _viewModel.IsAudioSubtitlesOpen || _viewModel.IsPlaybackStatsOpen || _viewModel.IsNextEpisodeOverlayOpen || _viewModel.IsEpisodesDrawerOpen ||
            SeekSlider.IsMouseCaptureWithin || VolumeSlider.IsMouseCaptureWithin ||
            AudioSubtitlesOverlay.IsMouseOver || PlaybackStatsOverlay.IsMouseOver || NextEpisodeOverlay.IsMouseOver || EpisodesDrawerOverlay.IsMouseOver ||
            AudioSubtitlesOverlay.IsKeyboardFocusWithin || PlaybackStatsOverlay.IsKeyboardFocusWithin || EpisodesDrawerOverlay.IsKeyboardFocusWithin;
        if (!_viewModel.CanHideControls || interacting || !IsEnabled || WindowState == WindowState.Minimized)

        {
            RevealControls();
            return;
        }
        if (!isCursorInsidePlayer) PlayerOverlay.Cursor = Cursors.Arrow;
        if (DateTime.UtcNow - _lastInteraction >= TimeSpan.FromSeconds(2.5))
        {
            PlayerChrome.Visibility = PlayerControls.Visibility = Visibility.Collapsed;
            PipHeader.Visibility = Visibility.Collapsed;
            PlayerOverlay.Cursor = isCursorInsidePlayer ? Cursors.None : Cursors.Arrow;
        }
    }
    private void UpdateTimers()
    {
        if (_closing) return;
        _controlsTimer.IsEnabled = _viewModel.ShowTv && _viewModel.CanHideControls && WindowState != WindowState.Minimized;
        _mediaTimer.Interval = WindowState == WindowState.Minimized ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(2);
        _mediaTimer.IsEnabled = _viewModel.CanStopPlayback;
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
        PlayerOverlay.Cursor = Cursors.Arrow;
        _controlsTimer.Stop();
        _mediaTimer.Stop();
        _osdTimer.Stop();
        _videoClickTimer.Stop();
        _categoryFocusOperation?.Abort();
        _viewModel.ThemeChanged -= ApplyTheme;
        _viewModel.UiScaleChanged -= ApplyUiScale;
        SystemEvents.PowerModeChanged -= PowerModeChanged;
        _viewModel.PropertyChanged -= ViewModelChanged;
        VideoView.MediaPlayer = null;
        await _viewModel.DisposeAsync();
        _closeComplete = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_overlayWindow is not null)
        {
            _overlayWindow.Deactivated -= PlayerWindow_Deactivated;
            _overlayWindow.Activated -= PlayerWindow_Activated;
        }
        VideoView.MediaPlayer = null;
        VideoView.Dispose();
        base.OnClosed(e);
    }
}
