using System.Net;
using System.Net.Sockets;
using System.Text;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

public sealed class NativeHttpPlaybackTests
{
    [Theory]
    [InlineData(401, PlaybackFailure.Unauthorized)]
    [InlineData(403, PlaybackFailure.Forbidden)]
    [InlineData(404, PlaybackFailure.NotFound)]
    public async Task ActualNativeHttpDenialIsClassifiedWithoutControllerRetries(int status, PlaybackFailure expected)
    {
        using var cancel = new CancellationTokenSource();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = ServeAsync(listener, status, cancel.Token);
        var backend = new LibVlcBackend();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10)));
        var loading = 0; service.StateChanged += (_, state) => { if (state.State == PlaybackState.Loading) Interlocked.Increment(ref loading); };
        try
        {
            await service.PlayAsync(new Uri($"http://127.0.0.1:{port}/live/fixture-user/fixture-password/1.ts"));
            await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Failed);
            Assert.Equal(expected, service.Snapshot.Failure);
            await Task.Delay(100);
            Assert.Equal(1, Volatile.Read(ref loading)); Assert.False(service.Snapshot.WillRetry);
        }
        finally { await service.DisposeAsync(); cancel.Cancel(); listener.Stop(); await server; }
    }

    private static async Task ServeAsync(TcpListener listener, int status, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(token);
                await using var stream = client.GetStream();
                // Consume only a bounded request header and never retain/log its URI.
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer, token);
                if (read == 0) continue;
                var reason = status switch { 401 => "Unauthorized", 403 => "Forbidden", _ => "Not Found" };
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (token.IsCancellationRequested) { }
    }
}
