using System.Runtime.InteropServices;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

public sealed class PlaybackRuntimeTests
{
    [Fact]
    public void DesktopOutputContainsNativeLibVlcForCurrentArchitecture()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "libvlc", RuntimeDirectory());
        Assert.True(File.Exists(Path.Combine(directory, "libvlc.dll")), directory);
        Assert.True(File.Exists(Path.Combine(directory, "libvlccore.dll")), directory);
        Assert.True(Directory.Exists(Path.Combine(directory, "plugins")), directory);
    }

    [Fact]
    public void PlaybackServiceInitializesAgainstBundledRuntime()
    {
        using var playback = new LibVlcPlaybackService();
        playback.Initialize();
        Assert.True(playback.IsReady);
    }

    private static string RuntimeDirectory() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            var architecture => throw new PlatformNotSupportedException($"Arquitectura no soportada: {architecture}.")
        };
}
