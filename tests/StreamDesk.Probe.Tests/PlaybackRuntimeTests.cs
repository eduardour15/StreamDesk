using System.Runtime.InteropServices;
using StreamDesk.Probe.Playback;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using LibVLCSharp.Shared;

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
    public async Task PlaybackServiceInitializesAgainstBundledRuntime()
    {
        await using var playback = new PlaybackService(new LibVlcBackend());
        await playback.InitializeAsync();
        var handle = playback.PlayerHandle;
        await playback.InitializeAsync();
        Assert.Same(handle, playback.PlayerHandle);
        Assert.True(playback.IsReady);
        Assert.False(playback.HasActiveMedia);
        await playback.StopAsync(); await playback.DisposeAsync(); await playback.DisposeAsync();
        Assert.Null(playback.PlayerHandle);
        Assert.False(playback.IsReady);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => playback.InitializeAsync());
    }

    private static string RuntimeDirectory() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            var architecture => throw new PlatformNotSupportedException($"Arquitectura no soportada: {architecture}.")
        };

    [Fact]
    public async Task NativeDecoderProducesFramesAcrossTwentyLocalFixturesAndCloses()
    {
        using var directory = new TestDatabase();
        var fixtures = new List<string>();
        for (var channel = 0; channel < 20; channel++)
        {
            var path = Path.Combine(directory.Root, $"invented-frame-{channel}.bmp"); fixtures.Add(path);
            using var output = new BinaryWriter(File.Create(path));
            output.Write((ushort)0x4D42); output.Write(54 + 16 * 16 * 3); output.Write(0); output.Write(54);
            output.Write(40); output.Write(16); output.Write(16); output.Write((ushort)1); output.Write((ushort)24);
            output.Write(0); output.Write(16 * 16 * 3); output.Write(2835); output.Write(2835); output.Write(0); output.Write(0);
            for (var pixel = 0; pixel < 16 * 16; pixel++) output.Write(new byte[] { 32, 192, (byte)(64 + channel) });
        }
        var memory = Marshal.AllocHGlobal(16 * 16 * 4 + 31);
        var aligned = new nint((memory.ToInt64() + 31) & ~31L);
        var frames = 0; var pixelValue = 0;
        var service = new PlaybackService(new LibVlcBackend()) { Options = new(250, HardwareDecoding.Software), Volume = 0 };
        try
        {
            await service.InitializeAsync();
            var player = Assert.IsType<MediaPlayer>(service.PlayerHandle);
            player.SetVideoFormat("RV32", 16, 16, 64);
            player.SetVideoCallbacks((_, planes) => { Marshal.WriteIntPtr(planes, aligned); return nint.Zero; }, null,
                (_, _) => { Volatile.Write(ref pixelValue, Marshal.ReadInt32(aligned)); Interlocked.Increment(ref frames); });
            for (var index = 0; index < 20; index++)
            {
                await service.StopAsync();
                var previous = Volatile.Read(ref frames);
                await service.PlayAsync(new Uri(fixtures[index]));
                await PlaybackTests.UntilAsync(() => Volatile.Read(ref frames) > previous);
                Assert.Equal(32 | (192 << 8) | ((64 + index) << 16), Volatile.Read(ref pixelValue) & 0x00FFFFFF);
                Assert.Same(player, service.PlayerHandle);
            }
            await service.StopAsync(); Assert.False(service.HasActiveMedia);
            await service.DisposeAsync(); Assert.Null(service.PlayerHandle);
        }
        finally { await service.DisposeAsync(); Marshal.FreeHGlobal(memory); }
    }
}
