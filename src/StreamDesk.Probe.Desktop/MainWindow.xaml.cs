using System.Windows;
using LibVLCSharp.Shared;
using Wpf.Ui.Controls;

namespace StreamDesk.Probe.Desktop;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private bool _isFullscreen;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private WindowState _previousWindowState;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        PasswordBox.Clear();
        await _viewModel.ConnectAsync(BaseUrlBox.Text, UsernameBox.Text, password);
    }

    private async void LoadCatalog_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadCatalogAsync();

    private void PlaySelected_Click(object sender, RoutedEventArgs e) => _viewModel.PlaySelected();

    private void Stop_Click(object sender, RoutedEventArgs e) => _viewModel.StopPlayback();

    private void VideoView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.MediaPlayer is MediaPlayer mediaPlayer)
        {
            VideoView.MediaPlayer = mediaPlayer;
        }
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
