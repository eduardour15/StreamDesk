using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StreamDesk.Probe.Desktop;

public sealed record DisplayChoice(nint Handle, string Name);

public sealed class WindowDisplays
{
    private Placement _restore;
    private WindowStyle _style;
    private ResizeMode _resize;
    private Placement _pictureRestore;
    private WindowStyle _pictureStyle;
    private ResizeMode _pictureResize;
    private double _minimumWidth, _minimumHeight;
    private bool _previousTopmost;
    public bool IsFullscreen { get; private set; }
    public bool IsPictureInPicture { get; private set; }
    public static IReadOnlyList<DisplayChoice> Enumerate()
    {
        var choices = new List<DisplayChoice>();
        MonitorCallback callback = (nint monitor, nint dc, ref NativeRect bounds, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info)) choices.Add(new(monitor, $"Pantalla {choices.Count + 1}" + ((info.Flags & 1) != 0 ? " (principal)" : "")));
            return true;
        };
        EnumDisplayMonitors(0, 0, callback, 0);
        return choices;
    }
    public void Toggle(Window window, DisplayChoice? display)
    {
        if (IsPictureInPicture) TogglePictureInPicture(window);
        var handle = new WindowInteropHelper(window).EnsureHandle();
        if (IsFullscreen)
        {
            window.WindowStyle = _style; window.ResizeMode = _resize;
            if (!SetWindowPlacement(handle, ref _restore)) throw new Win32Exception();
            IsFullscreen = false; return;
        }
        var monitor = display?.Handle ?? MonitorFromWindow(handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) { monitor = MonitorFromWindow(handle, 2); if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(); }
        _restore = new Placement { Length = Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(handle, ref _restore)) throw new Win32Exception();
        _style = window.WindowStyle; _resize = window.ResizeMode;
        window.WindowState = WindowState.Normal; window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize;
        var rect = info.Bounds;
        if (!SetWindowPos(handle, 0, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, 0x0020 | 0x0004))
        { window.WindowStyle = _style; window.ResizeMode = _resize; SetWindowPlacement(handle, ref _restore); throw new Win32Exception(); }
        IsFullscreen = true;
    }

    public void TogglePictureInPicture(Window window)
    {
        if (IsFullscreen) Toggle(window, null);
        var handle = new WindowInteropHelper(window).EnsureHandle();
        if (IsPictureInPicture)
        {
            RestorePicturePlacement(window, handle);
            IsPictureInPicture = false;
            return;
        }
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) throw new Win32Exception();
        _pictureRestore = new Placement { Length = Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(handle, ref _pictureRestore)) throw new Win32Exception();
        _pictureStyle = window.WindowStyle; _pictureResize = window.ResizeMode;
        _minimumWidth = window.MinWidth; _minimumHeight = window.MinHeight; _previousTopmost = window.Topmost;
        var scale = System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
        var gap = (int)Math.Round(12 * scale);
        var width = Math.Min((int)Math.Round(480 * scale), info.Work.Right - info.Work.Left - 2 * gap);
        var height = Math.Min((int)Math.Round(340 * scale), info.Work.Bottom - info.Work.Top - 2 * gap);
        try
        {
            IsPictureInPicture = true;
            window.MinWidth = 360; window.MinHeight = 260;
            window.WindowState = WindowState.Normal; window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.ResizeMode = ResizeMode.CanResize; window.Topmost = true;
            if (!SetWindowPos(handle, 0, info.Work.Right - width - gap, info.Work.Bottom - height - gap, width, height, 0x0020 | 0x0004)) throw new Win32Exception();
        }
        catch
        {
            IsPictureInPicture = false;
            RestorePicturePlacement(window, handle);
            throw;
        }
    }

    private void RestorePicturePlacement(Window window, nint handle)
    {
        window.WindowStyle = _pictureStyle; window.ResizeMode = _pictureResize;
        window.MinWidth = _minimumWidth; window.MinHeight = _minimumHeight; window.Topmost = _previousTopmost;
        if (!SetWindowPlacement(handle, ref _pictureRestore)) throw new Win32Exception();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Placement { public int Length, Flags, ShowCommand; public NativePoint Minimum, Maximum; public NativeRect Normal; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect rectangle, nint data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
