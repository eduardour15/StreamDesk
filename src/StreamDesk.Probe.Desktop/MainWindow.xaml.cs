using System.Windows;
using LibVLCSharp.Shared;
using StreamDesk.Probe.Core;
using Wpf.Ui.Controls;

namespace StreamDesk.Probe.Desktop;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _isFullscreen;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private WindowState _previousWindowState;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(services.ProviderFactory, services.Playback, services.Profiles, services.Logger);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadProfilesAsync();
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

    private void PlaySelected_Click(object sender, RoutedEventArgs e) => _viewModel.PlaySelected();
    private void Stop_Click(object sender, RoutedEventArgs e) => _viewModel.StopPlayback();

    private void VideoView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PlayerHandle is MediaPlayer mediaPlayer) VideoView.MediaPlayer = mediaPlayer;
    }

    private void ToggleFullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!_isFullscreen)
        {
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            _previousWindowState = WindowState;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
            return;
        }
        WindowStyle = _previousWindowStyle;
        ResizeMode = _previousResizeMode;
        WindowState = _previousWindowState;
        _isFullscreen = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        VideoView.Dispose();
        base.OnClosed(e);
    }
}
