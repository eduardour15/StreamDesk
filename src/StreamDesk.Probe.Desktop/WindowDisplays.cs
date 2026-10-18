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
    public bool IsFullscreen { get; private set; }
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
