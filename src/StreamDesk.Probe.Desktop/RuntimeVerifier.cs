using System.IO;
using System.Runtime.InteropServices;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Desktop;

internal static class RuntimeVerifier
{
    public static async Task VerifyAsync()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "streamdesk-runtime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        var path = Path.Combine(temporary, "fixture.bmp");
        var memory = Marshal.AllocHGlobal(16 * 16 * 4 + 31); var aligned = new nint((memory.ToInt64() + 31) & ~31L);
        var frames = 0; var pixel = 0;
        var playback = new PlaybackService(new LibVlcBackend()) { Options = new(250, HardwareDecoding.Software), Volume = 0 };
        try
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write((ushort)0x4D42); writer.Write(54 + 16 * 16 * 3); writer.Write(0); writer.Write(54);
                writer.Write(40); writer.Write(16); writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)24);
                writer.Write(0); writer.Write(16 * 16 * 3); writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
                for (var i = 0; i < 16 * 16; i++) writer.Write(new byte[] { 32, 192, 64 });
            }
            await playback.InitializeAsync();
            var player = (LibVLCSharp.Shared.MediaPlayer)playback.PlayerHandle!;
            player.SetVideoFormat("RV32", 16, 16, 64);
            player.SetVideoCallbacks((_, planes) => { Marshal.WriteIntPtr(planes, aligned); return nint.Zero; }, null,
                (_, _) => { Volatile.Write(ref pixel, Marshal.ReadInt32(aligned)); Interlocked.Increment(ref frames); });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await playback.PlayAsync(new Uri(path), timeout.Token);
            while (Volatile.Read(ref frames) == 0) await Task.Delay(10, timeout.Token);
            if ((Volatile.Read(ref pixel) & 0x00FFFFFF) != (32 | (192 << 8) | (64 << 16))) throw new InvalidOperationException("Decodificación local no válida.");
            await playback.StopAsync();
        }
        finally
        {
            await playback.DisposeAsync(); Marshal.FreeHGlobal(memory);
            var resolved = Path.GetFullPath(temporary); var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("streamdesk-runtime-", StringComparison.Ordinal)) Directory.Delete(resolved, true);
        }
    }
}
