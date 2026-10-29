using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using LibVLCSharp.Shared;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

[Collection("NativePlayback")]
public sealed class NativeLiveQualityTests
{
    [Fact]
    public async Task ActualHlsHonorsDataSaverAndPlaysSingleVariantWithoutAnotherPlayer()
    {
        using var directory = new TestDatabase();
        var avi = Path.Combine(directory.Root, "quality.avi"); NativeVideoFixture.Write(avi);
        var ts = Path.Combine(directory.Root, "quality.ts");
        var backend = new LibVlcBackend(); var service = new PlaybackService(backend) { Volume = 0 };
        var memory = Marshal.AllocHGlobal(160 * 90 * 4 + 31);
        var aligned = new nint((memory.ToInt64() + 31) & ~31L);
        var frames = 0;
        try
        {
            await service.InitializeAsync(); await EncodeFixtureAsync(avi, ts);
            await using var server = new FixtureHlsServer(await File.ReadAllBytesAsync(ts));
            var player = Assert.IsType<MediaPlayer>(service.PlayerHandle);
            player.SetVideoFormat("RV32", 160, 90, 160 * 4);
            player.SetVideoCallbacks((_, planes) => { Marshal.WriteIntPtr(planes, aligned); return nint.Zero; }, null,
                (_, _) => Interlocked.Increment(ref frames));
            foreach (var (quality, path) in new[]
            {
                (LiveQualityPreference.Automatic, "master.m3u8"),
                (LiveQualityPreference.DataSaver, "master.m3u8"),
                (LiveQualityPreference.DataSaver, "single.m3u8")
            })
            {
                await service.StopAsync(); server.Requests.Clear();
                service.Options = new(5000, HardwareDecoding.Software, PrioritizeContinuity: true, LiveQuality: quality);
                var before = Volatile.Read(ref frames);
                await service.PlayAsync(new Uri(server.BaseUri, path));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (Volatile.Read(ref frames) < before + 2) await Task.Delay(20, timeout.Token);
                Assert.Same(player, service.PlayerHandle); Assert.Equal(0, service.Snapshot.RetryCount);
                if (quality == LiveQualityPreference.DataSaver && path == "master.m3u8")
                { Assert.Contains("/low.ts", server.Requests); Assert.DoesNotContain("/high.ts", server.Requests); }
                if (path == "single.m3u8") Assert.Contains("/high.ts", server.Requests);
            }
            await service.StopAsync(); Assert.False(service.HasActiveMedia);
        }
        finally { await service.DisposeAsync(); Marshal.FreeHGlobal(memory); }
    }

    private static async Task EncodeFixtureAsync(string input, string output)
    {
        using var lib = new LibVLC("--no-video-title-show"); lib.Log += (_, _) => { };
        using var media = new Media(lib, new Uri(input));
        media.AddOption(":stop-time=6");
        media.AddOption(":sout=#transcode{vcodec=mp2v,vb=128,fps=25,width=160,height=90}:std{access=file,mux=ts,dst='" + output.Replace('\\', '/') + "'}");
        using var player = new MediaPlayer(lib);
        var ended = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.EndReached += (_, _) => ended.TrySetResult(true);
        player.EncounteredError += (_, _) => ended.TrySetResult(false);
        try
        {
            Assert.True(player.Play(media));
            Assert.True(await ended.Task.WaitAsync(TimeSpan.FromSeconds(20)), "Bundled VLC could not encode the local TS fixture.");
        }
        finally { await Task.Run(player.Stop); }
        Assert.True(new FileInfo(output).Length > 188);
    }

    private sealed class FixtureHlsServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancel = new();
        private readonly Task _serving;
        private readonly byte[] _segment;
        public ConcurrentQueue<string> Requests { get; } = new();
        public Uri BaseUri { get; }
        public FixtureHlsServer(byte[] segment)
        {
            _segment = segment; _listener.Start();
            BaseUri = new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/"); _serving = ServeAsync();
        }
        private async Task ServeAsync()
        {
            try
            {
                while (!_cancel.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_cancel.Token);
                    try
                    {
                        await using var stream = client.GetStream(); var request = new byte[4096];
                        var read = await stream.ReadAsync(request, _cancel.Token);
                        if (read == 0) continue;
                        var path = Encoding.ASCII.GetString(request, 0, read).Split(' ')[1].Split('?')[0]; Requests.Enqueue(path);
                        var manifest = path switch
                        {
                            "/master.m3u8" => "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=128000\nlow.m3u8\n#EXT-X-STREAM-INF:BANDWIDTH=512000\nhigh.m3u8\n",
                            "/low.m3u8" => Playlist("low.ts"),
                            _ => Playlist("high.ts")
                        };
                        var segment = path.EndsWith(".ts", StringComparison.Ordinal);
                        var body = segment ? _segment : Encoding.UTF8.GetBytes(manifest);
                        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {(segment ? "video/mp2t" : "application/vnd.apple.mpegurl")}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, _cancel.Token); await stream.WriteAsync(body, _cancel.Token);
                    }
                    catch (IOException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (_cancel.IsCancellationRequested) { }
        }
        private static string Playlist(string segment) => "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:6\n#EXT-X-MEDIA-SEQUENCE:0\n#EXTINF:6.0,\n" + segment + "\n#EXT-X-ENDLIST\n";
        public async ValueTask DisposeAsync() { _cancel.Cancel(); _listener.Stop(); await _serving; _cancel.Dispose(); }
    }
}
