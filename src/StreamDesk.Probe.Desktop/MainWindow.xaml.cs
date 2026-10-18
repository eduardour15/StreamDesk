using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Win32;
using LibVLCSharp.Shared;
using StreamDesk.Probe.Core;
using Wpf.Ui.Controls;

namespace StreamDesk.Probe.Desktop;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly WindowDisplays _displays = new();
    private bool _closing, _closeComplete;
    public IReadOnlyList<DisplayChoice> Displays { get; } = WindowDisplays.Enumerate();
    public DisplayChoice? SelectedDisplay { get; set; }
    public ChannelLogoLoader LogoLoader { get; }

    public MainWindow(AppServices services) : this(
        new MainViewModel(services.ProviderFactory, services.Playback, services.Profiles, services.Logger, services.Catalogs, services.Credentials), services.Logos)
    { }

    public MainWindow(MainViewModel viewModel, ChannelLogoLoader logoLoader)
    {
        _viewModel = viewModel;
        LogoLoader = logoLoader;
        SelectedDisplay = Displays.FirstOrDefault();
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => { await _viewModel.InitializePlaybackAsync(); if (!_closing) await _viewModel.LoadProfilesAsync(); };
        _viewModel.PropertyChanged += ViewModelChanged;
        SystemEvents.PowerModeChanged += PowerModeChanged;
        Closing += WindowClosing;
        PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape && _displays.IsFullscreen) { ToggleFullscreen_Click(this, new RoutedEventArgs()); args.Handled = true; } };
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        PasswordBox.Clear();
        await _viewModel.ConnectAsync(BaseUrlBox.Text, UsernameBox.Text, password);
    }

    private async void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        var password = ProfilePasswordBox.Password;
        ProfilePasswordBox.Clear();
        await _viewModel.CreateProfileAsync(password);
    }

    private async void PlaySelected_Click(object sender, RoutedEventArgs e) => await _viewModel.PlaySelectedAsync();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await _viewModel.StopPlaybackAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _viewModel.NavigateChannelAsync(-1);
    private async void Next_Click(object sender, RoutedEventArgs e) => await _viewModel.NavigateChannelAsync(1);
    private void CancelCatalog_Click(object sender, RoutedEventArgs e) => _viewModel.CancelCatalog();

    private void VideoView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PlayerHandle is MediaPlayer mediaPlayer) VideoView.MediaPlayer = mediaPlayer;
    }

    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
    {
        try { _displays.Toggle(this, SelectedDisplay); }
        catch (Exception exception) { _viewModel.ReportWindowError(exception); }
    }

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs args)
    { if (!_closing && args.PropertyName == nameof(MainViewModel.PlayerHandle)) VideoView_Loaded(this, new RoutedEventArgs()); }

    private void PowerModeChanged(object sender, PowerModeChangedEventArgs args)
    { if (args.Mode == PowerModes.Suspend && !_closing) _ = _viewModel.SuspendPlaybackAsync(); }

    private async void WindowClosing(object? sender, CancelEventArgs args)
    {
        if (_closeComplete) return;
        args.Cancel = true;
        if (_closing) return;
        _closing = true;
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
