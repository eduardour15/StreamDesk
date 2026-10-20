using System.Windows.Input;

namespace StreamDesk.Probe.Desktop;

public enum PlaybackShortcut { None, Toggle, ExitFullscreen, Previous, Next, VolumeUp, VolumeDown, Fullscreen, Mute, PictureInPicture, ExitPictureInPicture, SeekBackward, SeekForward }
public static class PlaybackShortcuts
{
    public static PlaybackShortcut Resolve(Key key, ModifierKeys modifiers, bool editing, bool fullscreen, bool tv, bool pictureInPicture = false)
    {
        if (key == Key.Escape && pictureInPicture) return PlaybackShortcut.ExitPictureInPicture;
        if (key == Key.Escape && fullscreen) return PlaybackShortcut.ExitFullscreen;
        if (editing || !tv || modifiers != ModifierKeys.None) return PlaybackShortcut.None;
        return key switch
        {
            Key.Space or Key.K => PlaybackShortcut.Toggle, Key.Left => PlaybackShortcut.Previous, Key.Right => PlaybackShortcut.Next,
            Key.Up => PlaybackShortcut.VolumeUp, Key.Down => PlaybackShortcut.VolumeDown,
            Key.J => PlaybackShortcut.SeekBackward, Key.L => PlaybackShortcut.SeekForward,
            Key.F => PlaybackShortcut.Fullscreen, Key.M => PlaybackShortcut.Mute, Key.P => PlaybackShortcut.PictureInPicture, _ => PlaybackShortcut.None
        };
    }
}
