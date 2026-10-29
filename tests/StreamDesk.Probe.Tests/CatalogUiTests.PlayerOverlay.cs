using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed partial class CatalogUiTests
{
    private static async Task VerifyOverlayNavigationAsync(string artifacts, MainWindow window, MainViewModel vm,
        LibVLCSharp.Shared.MediaPlayer player)
    {
        var root = (FrameworkElement)window.FindName("PlayerOverlay");
        var overlay = Window.GetWindow(root);
        var video = window.FindName("VideoView");
        var hwnd = player.Hwnd;
        using var media = player.Media;
        foreach (var tab in new[] { 0, 4, 5, 6 })
        {
            vm.CurrentTabIndex = tab; await SettleAsync(window);
            Assert.False(overlay.IsVisible, $"Overlay leaked onto page {tab}.");
            Assert.False(IsWindowVisible(new WindowInteropHelper(overlay).Handle));
            Assert.False(root.IsVisible);
            Assert.Equal(Cursors.Arrow, root.Cursor);
            // Activation or a player notification must not resurrect the hidden overlay.
            window.Activate(); vm.Volume = vm.Volume == 75 ? 76 : 75; await SettleAsync(window);
            Assert.False(overlay.IsVisible);
            if (tab == 0)
            {
                var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render((Visual)window.Content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var file = File.Create(Path.Combine(artifacts, "player-home-layout.png")); encoder.Save(file);
            }
            if (tab == 0 && Environment.GetEnvironmentVariable("STREAMDESK_NATIVE_INPUT") == "1")
                CapturePlayerScreen((FrameworkElement)window.Content, Path.Combine(artifacts, "player-home-without-overlay.png"));
            vm.CurrentTabIndex = 1; await SettleAsync(window);
            Assert.True(overlay.IsVisible); Assert.True(root.IsVisible);
            Assert.Same(video, window.FindName("VideoView")); Assert.Equal(hwnd, player.Hwnd);
            Assert.True(vm.IsPlaying);
            using var current = player.Media;
            Assert.Equal(media?.NativeReference, current?.NativeReference);
        }
        await File.WriteAllTextAsync(Path.Combine(artifacts, "player-overlay-navigation-evidence.json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Source = "WPF + LibVLC nativo con AVI local generado",
                InitialHomeOverlayHidden = true, HiddenPages = new[] { "Inicio", "Ajustes", "Películas", "Series" },
                ReturnedToPlayer = true, SameNativeHwndAndMedia = true, NativeWindowVisibilityVerified = true,
                HomeCapture = "WPF layout; foreground HWND hidden verified separately with IsWindowVisible", UserProvider = "PENDIENTE_DE_VALIDACION"
            }));
    }

    private static void PressOverlay(MainWindow window, Key key)
    {
        var root = (UIElement)window.FindName("PlayerOverlay");
        root.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(root), 0, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }

    private static async Task VerifyPlayerKeyboardAsync(MainWindow window, MainViewModel vm, ControlledBackend backend)
    {
        var opens = backend.Opens.Count;
        PressOverlay(window, Key.Space); await WaitAsync(() => vm.IsPaused);
        PressOverlay(window, Key.K); await WaitAsync(() => vm.IsPlaying);
        Assert.Equal(opens, backend.Opens.Count);
        PressOverlay(window, Key.Right); await WaitAsync(() => backend.Sought == 65000);
        PressOverlay(window, Key.Left); await WaitAsync(() => backend.Sought == 60000);
        PressOverlay(window, Key.J); await WaitAsync(() => backend.Sought == 50000);
        PressOverlay(window, Key.L); await WaitAsync(() => backend.Sought == 60000);
        await vm.SeekToAsync(1); PressOverlay(window, Key.Left); await WaitAsync(() => backend.Sought == 0);
        await vm.SeekToAsync(vm.DurationSeconds - 1); PressOverlay(window, Key.Right);
        await WaitAsync(() => backend.Sought == (long)(vm.DurationSeconds * 1000));
        vm.Volume = 98; PressOverlay(window, Key.Up); Assert.Equal(100, vm.Volume);
        PressOverlay(window, Key.Up); Assert.Equal(100, vm.Volume);
        vm.Volume = 2; PressOverlay(window, Key.Down); Assert.Equal(0, vm.Volume);
        PressOverlay(window, Key.Down); Assert.Equal(0, vm.Volume);
        var muted = vm.IsMuted; PressOverlay(window, Key.M); Assert.NotEqual(muted, vm.IsMuted);
        var slider = (Slider)window.FindName("SeekSlider");
        var sought = backend.Sought;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(slider), 0, Key.Right)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        slider.RaiseEvent(args);
        Assert.False(args.Handled); Assert.Equal(sought, backend.Sought);
        await vm.RefreshPlaybackDetailsAsync();
    }

    private static async Task VerifyNativeOverlayInteractionAsync(string artifacts, MainWindow window, MainViewModel vm,
        LibVLCSharp.Shared.MediaPlayer player)
    {
        var root = (FrameworkElement)window.FindName("PlayerOverlay");
        var controls = (FrameworkElement)window.FindName("PlayerControls");
        var chrome = (FrameworkElement)window.FindName("PlayerChrome");
        var video = (FrameworkElement)window.FindName("VideoView");
        var overlayWindow = Window.GetWindow(root);
        var hwnd = player.Hwnd;
        // Desktop injection is opt-in; regular CI verifies WPF/native geometry without moving the user's pointer.
        var desktopInput = Environment.GetEnvironmentVariable("STREAMDESK_NATIVE_INPUT") == "1";
        var originalCursor = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (desktopInput) Assert.True(GetCursorInfo(ref originalCursor));
        try
        {
            // Actual mouse hit-testing crosses LibVLCSharp's independent foreground HWND.
            window.Left = SystemParameters.WorkArea.Left + 40; window.Top = SystemParameters.WorkArea.Top + 40;
            window.Activate(); overlayWindow.Activate(); await SettleAsync(window);
            var button = (FrameworkElement)window.FindName("PlayPauseButton");
            var click = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            if (desktopInput)
            {
                Assert.True(window.IsActive || overlayWindow.IsActive, "Desktop input requires an active test window.");
                Assert.Equal(new WindowInteropHelper(overlayWindow).Handle, WindowFromPoint(new NativePoint { X = (int)click.X, Y = (int)click.Y }));
                CapturePlayerScreen((FrameworkElement)window.Content, Path.Combine(artifacts, "player-overlay-before-click.png"));
                Assert.True(SetCursorPos((int)click.X, (int)click.Y));
                mouse_event(0x0002, 0, 0, 0, 0); mouse_event(0x0004, 0, 0, 0, 0);
            }
            else ((ButtonBase)button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitAsync(() => vm.IsPaused);
            Assert.Equal(Cursors.Arrow, root.Cursor);
            var normal = window.RestoreBounds;
            for (var round = 0; round < 3; round++)
            {
                PressOverlay(window, Key.F); await SettleAsync(window);
                var client = new NativeRect(); Assert.True(GetClientRect(new WindowInteropHelper(window).Handle, out client));
                var bounds = BoundsIn(video, window);
                Assert.InRange(Math.Abs(bounds.Width - client.Right), 0, 1);
                Assert.InRange(Math.Abs(bounds.Height - client.Bottom), 0, 1);
                Assert.InRange(Math.Abs(bounds.Left) + Math.Abs(bounds.Top), 0, 1);
                Assert.Equal(hwnd, player.Hwnd);
                PressOverlay(window, Key.Escape); await SettleAsync(window); AssertBounds(normal, window.RestoreBounds);
            }
            PressOverlay(window, Key.F); await SettleAsync(window);
            overlayWindow.Activate(); Keyboard.Focus(root);
            var center = root.PointToScreen(new Point(root.ActualWidth / 2, root.ActualHeight / 3));
            if (desktopInput)
            {
                Assert.True(SetCursorPos((int)center.X, (int)center.Y));
                keybd_event(0x20, 0, 0, 0); keybd_event(0x20, 0, 2, 0);
            }
            else PressOverlay(window, Key.Space);
            await WaitAsync(() => vm.IsPlaying);
            await SettleAsync(window);
            if (desktopInput) CapturePlayerScreen(video, Path.Combine(artifacts, "player-overlay-visible.png"));
            var visibleBounds = BoundsIn(video, window);
            await WaitAsync(() => chrome.Visibility == Visibility.Collapsed);
            Assert.Equal(Visibility.Collapsed, controls.Visibility);
            Assert.Equal(visibleBounds, BoundsIn(video, window));
            if (desktopInput)
            {
                Assert.Equal(Cursors.None, root.Cursor);
                await WaitAsync(() =>
                {
                    var hiddenCursor = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
                    return GetCursorInfo(ref hiddenCursor) && (hiddenCursor.Flags & 1u) == 0;
                });
                CapturePlayerScreen(video, Path.Combine(artifacts, "player-overlay-hidden.png"));
                Assert.True(SetCursorPos((int)center.X + 30, (int)center.Y));
            }
            else root.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
            await WaitAsync(() => root.Cursor == Cursors.Arrow && chrome.Visibility == Visibility.Visible);
            // Seeking a live channel with no timeline never opens another medium or seeks it.
            Assert.False(vm.CanSeek); using var initialMedia = player.Media;
            PressOverlay(window, Key.Left); PressOverlay(window, Key.Right); PressOverlay(window, Key.J); PressOverlay(window, Key.L);
            using var currentMedia = player.Media;
            Assert.Equal(initialMedia?.NativeReference, currentMedia?.NativeReference); Assert.Equal(hwnd, player.Hwnd);
            Assert.True(currentMedia?.Statistics.DecodedVideo >= 2);
            Assert.True(player.Time >= 1000);
            PressOverlay(window, Key.Escape); await SettleAsync(window);
            window.WindowState = WindowState.Minimized; await SettleAsync(window);
            Assert.Equal(Cursors.Arrow, root.Cursor);
            window.WindowState = WindowState.Normal; await SettleAsync(window);
            await File.WriteAllTextAsync(Path.Combine(artifacts, desktopInput ? "player-overlay-desktop-evidence.json" : "player-overlay-evidence.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Source = "WPF + LibVLC nativo con vídeo AVI local generado (16:9, 2 fps), no proveedor IPTV",
                NativeDecodedVideoFrames = currentMedia?.Statistics.DecodedVideo,
                OwnedTransparentOverlay = true, StableViewport = true, FullscreenRoundTrips = 3,
                SameNativeHwnd = true, AutoHideSeconds = 2.5, RoutedKeyboardAndControls = true,
                DesktopMouseKeyboardAndCursor = desktopInput ? "VERIFICADO" : "PENDIENTE_DE_VALIDACION",
                ProviderVideoAndMultiplePhysicalDpi = "PENDIENTE_DE_VALIDACION"
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        finally { if (desktopInput) SetCursorPos(originalCursor.Position.X, originalCursor.Position.Y); }
    }

    private static void CapturePlayerScreen(FrameworkElement video, string path)
    {
        var start = video.PointToScreen(new Point());
        var end = video.PointToScreen(new Point(video.ActualWidth, video.ActualHeight));
        var width = (int)Math.Round(end.X - start.X); var height = (int)Math.Round(end.Y - start.Y);
        var screen = GetDC(0); var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height); var previous = SelectObject(memory, bitmap);
        try
        {
            Assert.True(BitBlt(memory, 0, 0, width, height, screen, (int)start.X, (int)start.Y, 0x40CC0020));
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(path); encoder.Save(file);
        }
        finally { SelectObject(memory, previous); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(0, screen); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public int Size; public uint Flags; public nint Cursor; public NativePoint Position; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, nuint extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
}
