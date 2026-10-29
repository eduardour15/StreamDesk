using System.Windows.Input;

namespace StreamDesk.Probe.Desktop;

public enum PlaybackShortcut { None, Toggle, ExitFullscreen, Previous, Next, VolumeUp, VolumeDown, Fullscreen, Mute, PictureInPicture, ExitPictureInPicture, SeekBackward, SeekForward, SeekShortBackward, SeekShortForward, CycleBooster }
public static class PlaybackShortcuts
{
    public static PlaybackShortcut Resolve(Key key, ModifierKeys modifiers, bool editing, bool fullscreen, bool tv, bool pictureInPicture = false)
    {
        if (key == Key.Escape && modifiers == ModifierKeys.None && pictureInPicture) return PlaybackShortcut.ExitPictureInPicture;
        if (key == Key.Escape && modifiers == ModifierKeys.None && fullscreen) return PlaybackShortcut.ExitFullscreen;
        if (editing || !tv) return PlaybackShortcut.None;
        if (modifiers == ModifierKeys.Shift)
            return key switch { Key.P => PlaybackShortcut.Previous, Key.N => PlaybackShortcut.Next, _ => PlaybackShortcut.None };
        if (modifiers != ModifierKeys.None) return PlaybackShortcut.None;
        return key switch
        {
            Key.Space or Key.K => PlaybackShortcut.Toggle, Key.Left => PlaybackShortcut.SeekShortBackward, Key.Right => PlaybackShortcut.SeekShortForward,
            Key.Up => PlaybackShortcut.VolumeUp, Key.Down => PlaybackShortcut.VolumeDown,
            Key.J => PlaybackShortcut.SeekBackward, Key.L => PlaybackShortcut.SeekForward,
            Key.F => PlaybackShortcut.Fullscreen, Key.M => PlaybackShortcut.Mute, Key.P => PlaybackShortcut.PictureInPicture,
            Key.B => PlaybackShortcut.CycleBooster, _ => PlaybackShortcut.None
        };
    }
}
