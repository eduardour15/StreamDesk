using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed partial class CatalogUiTests
{
    private static async Task VerifyPictureInPictureAsync(string artifacts, ChannelLogoLoader logos)
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync();
        var credentials = new FixtureCredentials(); var backend = new ControlledBackend();
        var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(10)), new PlaybackService(backend),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        // Windows relocates off-screen restore rectangles. Use a visible position for exact placement checks.
        var work = SystemParameters.WorkArea;
        var window = new MainWindow(vm, logos) { Left = work.Left + 24, Top = work.Top + 24, Width = 1000, Height = 700,
            ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        try
        {
            window.Show(); await WaitAsync(() => vm.IsPlaybackReady && !vm.IsWorking);
            vm.CurrentTabIndex = 1; await vm.ConnectAsync("https://provider.invalid", "fixture-user", "fixture-password");
            await vm.LoadCatalogAsync(); vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync();
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await WaitAsync(() => vm.IsPlaying);
            await SettleAsync(window);
            var before = window.RestoreBounds; var minimum = (window.MinWidth, window.MinHeight);
            var video = window.FindName("VideoView"); var player = vm.PlayerHandle;
            for (var index = 0; index < 5; index++)
            {
                Click(window, "PipButton"); await SettleAsync(window);
                Assert.True(window.Topmost); AssertCompactControls(window);
                Assert.Same(video, window.FindName("VideoView")); Assert.Same(player, vm.PlayerHandle);
                Assert.Single(backend.Opens); Assert.Equal(1, backend.Active); Assert.Equal(1, backend.Peak);
                Click(window, "PipReturn"); await SettleAsync(window);
                Assert.False(window.Topmost); AssertBounds(before, window.RestoreBounds);
                Assert.Equal(minimum, (window.MinWidth, window.MinHeight));
            }
            Click(window, "PipButton"); await SettleAsync(window);
            var pin = (CheckBox)window.FindName("PipTopmost"); pin.IsChecked = false;
            Assert.False(window.Topmost);
            window.Width = 360; window.Height = 260; await SettleAsync(window); AssertCompactControls(window);
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render((Visual)window.Content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(artifacts, "f7-pip.png"))) encoder.Save(file);
            Press(window, Key.Escape); await SettleAsync(window); AssertBounds(before, window.RestoreBounds);

            // Both mode transitions must return to the original normal window.
            Click(window, "PipButton"); Press(window, Key.F); await SettleAsync(window);
            Assert.Equal(Visibility.Collapsed, ((UIElement)window.FindName("PipHeader")).Visibility);
            Press(window, Key.Escape); await SettleAsync(window); AssertBounds(before, window.RestoreBounds);
            Press(window, Key.F); Press(window, Key.P); await SettleAsync(window); AssertCompactControls(window);
            Press(window, Key.Escape); await SettleAsync(window); AssertBounds(before, window.RestoreBounds);

            window.Topmost = true; window.WindowState = WindowState.Maximized; await SettleAsync(window);
            Click(window, "PipButton"); await SettleAsync(window); pin.IsChecked = false;
            Press(window, Key.Escape); await SettleAsync(window);
            Assert.Equal(WindowState.Maximized, window.WindowState); Assert.True(window.Topmost);
            window.Topmost = false; window.WindowState = WindowState.Normal;
            Click(window, "PipButton"); vm.CurrentTabIndex = 4; await SettleAsync(window);
            Assert.Equal(Visibility.Collapsed, ((UIElement)window.FindName("PipHeader")).Visibility);
            Assert.False(window.Topmost); Assert.True(vm.IsPlaying);
            vm.CurrentTabIndex = 1; Click(window, "PipButton"); await SettleAsync(window);
            Assert.Single(backend.Opens); Assert.Equal(1, backend.Active);
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }
        Assert.Equal(0, backend.Active); Assert.Equal(1, backend.Disposals);
        await File.WriteAllTextAsync(Path.Combine(artifacts, "f7-pip-evidence.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Source = "WPF real con proveedor/backend ficticios", RoundTrips = 5, BackendOpens = backend.Opens.Count, PeakActive = backend.Peak,
            SameVideoView = true, SamePlayer = true, NormalPlacementRestored = true, MaximizedRestored = true, TopmostOptional = true,
            ModeTransitions = "PiP/fullscreen/normal", MinimumSize = "360x260 DIP", CloseDisposedBackend = true,
            PhysicalDpiAndProvider = "PENDIENTE"
        }));
    }

    private static async Task VerifyNativePictureInPictureAsync(MainWindow window, MainViewModel vm, LibVLCSharp.Shared.MediaPlayer player, string temporary)
    {
        var hwnd = player.Hwnd; var video = window.FindName("VideoView");
        Click(window, "PipButton"); await SettleAsync(window);
        AssertCompactControls(window); Assert.Equal(hwnd, player.Hwnd); Assert.Same(video, window.FindName("VideoView")); Assert.True(vm.IsPlaying);
        var path = Path.Combine(temporary, "pip-native.png");
        Assert.True(await Task.Run(() => player.TakeSnapshot(0, path, 16, 16)));
        await WaitAsync(() => File.Exists(path) && new FileInfo(path).Length > 0);
        using (var file = File.OpenRead(path))
        {
            var image = new FormatConvertedBitmap(BitmapFrame.Create(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[16 * 16 * 4]; image.CopyPixels(pixels, 16 * 4, 0);
            Assert.All(pixels.Where((_, index) => index % 4 != 3), value => Assert.InRange((int)value, 175, 185));
        }
        Click(window, "PipReturn"); await SettleAsync(window); Assert.Equal(hwnd, player.Hwnd); Assert.True(vm.IsPlaying);
    }

    private static void AssertCompactControls(MainWindow window)
    {
        Assert.Equal(Visibility.Visible, ((UIElement)window.FindName("PipHeader")).Visibility);
        Assert.Equal(Visibility.Collapsed, ((UIElement)window.FindName("StandardControls")).Visibility);
        var video = (FrameworkElement)window.FindName("VideoView");
        var videoBounds = video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize));
        foreach (var name in new[] { "PipHeader", "PipControls" })
        {
            var control = (FrameworkElement)window.FindName(name);
            Assert.True(control.IsVisible); var bounds = control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
            Assert.False(videoBounds.IntersectsWith(bounds));
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.ActualWidth + 1 && bounds.Bottom <= window.ActualHeight + 1, name + " exceeds compact layout.");
        }
    }
    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, 2); Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, 2);
        Assert.InRange(Math.Abs(expected.Width - actual.Width), 0, 2); Assert.InRange(Math.Abs(expected.Height - actual.Height), 0, 2);
    }
    private static void Click(MainWindow window, string name) => ((ButtonBase)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static void Press(MainWindow window, Key key) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static async Task SettleAsync(MainWindow window) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
}
