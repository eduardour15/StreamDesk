using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

[Collection("NativePlayback")]
public sealed class NativePlaybackStabilityTests
{
    [Fact]
    public async Task BundledVlcProducesChangingFramesWithAutomaticAndSoftwareSettingsOnTheSamePlayer()
    {
        using var directory = new TestDatabase();
        var path = Path.Combine(directory.Root, "stability-fixture.avi"); NativeVideoFixture.Write(path);
        var memory = Marshal.AllocHGlobal(32 * 18 * 4 + 31);
        var aligned = new nint((memory.ToInt64() + 31) & ~31L);
        var frames = 0; var pixel = 0;
        var backend = new LibVlcBackend(); var service = new PlaybackService(backend) { Volume = 0 };
        try
        {
            await service.InitializeAsync();
            var player = Assert.IsType<MediaPlayer>(service.PlayerHandle);
            player.SetVideoFormat("RV32", 32, 18, 32 * 4);
            player.SetVideoCallbacks((_, planes) => { Marshal.WriteIntPtr(planes, aligned); return nint.Zero; }, null,
                (_, _) => { Volatile.Write(ref pixel, Marshal.ReadInt32(aligned) & 0x00FFFFFF); Interlocked.Increment(ref frames); });
            foreach (var hardware in new[] { HardwareDecoding.Automatic, HardwareDecoding.Software, HardwareDecoding.Automatic })
            {
                await service.StopAsync();
                service.Options = new(3500, hardware);
                var previousFrames = Volatile.Read(ref frames);
                await service.PlayAsync(new Uri(path));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (Volatile.Read(ref frames) < previousFrames + 2) await Task.Delay(20, timeout.Token);
                var firstPixel = Volatile.Read(ref pixel);
                while (Volatile.Read(ref pixel) == firstPixel) await Task.Delay(20, timeout.Token);
                var progress = await backend.ReadProgressAsync(timeout.Token);
                Assert.True(progress!.VideoFrames > 0);
                Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
                Assert.Equal(0, service.Snapshot.RetryCount);
                Assert.Same(player, service.PlayerHandle);
            }
            await service.StopAsync(); Assert.False(service.HasActiveMedia);
            await service.DisposeAsync(); Assert.Null(service.PlayerHandle);
        }
        finally { await service.DisposeAsync(); Marshal.FreeHGlobal(memory); }
    }
}
