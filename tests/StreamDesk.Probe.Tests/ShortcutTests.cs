using System.Windows.Input;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class ShortcutTests
{
    [Theory]
    [InlineData(Key.Space, PlaybackShortcut.Toggle)]
    [InlineData(Key.K, PlaybackShortcut.Toggle)]
    [InlineData(Key.Left, PlaybackShortcut.SeekShortBackward)]
    [InlineData(Key.Right, PlaybackShortcut.SeekShortForward)]
    [InlineData(Key.J, PlaybackShortcut.SeekBackward)]
    [InlineData(Key.L, PlaybackShortcut.SeekForward)]
    [InlineData(Key.Up, PlaybackShortcut.VolumeUp)]
    [InlineData(Key.Down, PlaybackShortcut.VolumeDown)]
    [InlineData(Key.F, PlaybackShortcut.Fullscreen)]
    [InlineData(Key.M, PlaybackShortcut.Mute)]
    [InlineData(Key.P, PlaybackShortcut.PictureInPicture)]
    [InlineData(Key.B, PlaybackShortcut.CycleBooster)]
    public void ShortcutsRespectEditingModifiersAndOtherPages(Key key, PlaybackShortcut expected)
    {
        Assert.Equal(expected, PlaybackShortcuts.Resolve(key, ModifierKeys.None, false, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(key, ModifierKeys.None, true, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(key, ModifierKeys.Control, false, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(key, ModifierKeys.None, false, false, false));
    }
    [Fact]
    public void EscapeAlwaysRestoresFullscreenButOtherwiseIsUnclaimed()
    {
        Assert.Equal(PlaybackShortcut.ExitFullscreen, PlaybackShortcuts.Resolve(Key.Escape, ModifierKeys.None, true, true, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(Key.Escape, ModifierKeys.None, true, false, true));
    }
    [Fact]
    public void EscapeRestoresPictureInPictureEvenWithControlFocused()
    {
        Assert.Equal(PlaybackShortcut.ExitPictureInPicture, PlaybackShortcuts.Resolve(Key.Escape, ModifierKeys.None, true, false, true, pictureInPicture: true));
    }

    [Theory]
    [InlineData(Key.P, PlaybackShortcut.Previous)]
    [InlineData(Key.N, PlaybackShortcut.Next)]
    public void ChannelNavigationUsesShiftWithoutStealingSeekOrEditing(Key key, PlaybackShortcut expected)
    {
        Assert.Equal(expected, PlaybackShortcuts.Resolve(key, ModifierKeys.Shift, false, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(key, ModifierKeys.Shift, true, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(key, ModifierKeys.Control | ModifierKeys.Shift, false, false, true));
        Assert.Equal(PlaybackShortcut.None, PlaybackShortcuts.Resolve(key, ModifierKeys.Shift, false, false, false));
    }
}
