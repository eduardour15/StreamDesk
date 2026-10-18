using System.Windows.Input;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class ShortcutTests
{
    [Theory]
    [InlineData(Key.Space, PlaybackShortcut.Toggle)]
    [InlineData(Key.Left, PlaybackShortcut.Previous)]
    [InlineData(Key.Right, PlaybackShortcut.Next)]
    [InlineData(Key.Up, PlaybackShortcut.VolumeUp)]
    [InlineData(Key.Down, PlaybackShortcut.VolumeDown)]
    [InlineData(Key.F, PlaybackShortcut.Fullscreen)]
    [InlineData(Key.M, PlaybackShortcut.Mute)]
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
}
