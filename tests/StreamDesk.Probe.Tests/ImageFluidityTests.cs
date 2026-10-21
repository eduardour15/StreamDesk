using System.Net;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class ImageFluidityTests
{
    private static byte[] ImageBytes()
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(480, 720, 96, 96, PixelFormats.Bgra32, null, new byte[480 * 720 * 4], 480 * 4)));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }

    [Fact]
    public async Task SharedImageDownloadSurvivesOneConsumerCancellation()
    {
        var calls = 0; var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var bytes = ImageBytes();
        using var loader = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler(async (_, token) =>
        { Interlocked.Increment(ref calls); await gate.Task.WaitAsync(token); return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }; })));
        using var canceled = new CancellationTokenSource();
        var first = loader.GetAsync("https://images.invalid/poster.png", canceled.Token);
        var second = loader.GetAsync("https://images.invalid/poster.png", CancellationToken.None);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        gate.SetResult(); Assert.NotNull(await second);
        Assert.Equal(1, calls);
        Assert.Same(await second, await loader.GetAsync("https://images.invalid/poster.png", CancellationToken.None));
    }

    [Fact]
    public async Task LastConsumerCancellationAbortsDownloadAndTransientFailuresExpire()
    {
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var loader = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler(async (_, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { aborted.TrySetResult(); throw; }
            return new(HttpStatusCode.OK);
        }))))
        {
            using var cancel = new CancellationTokenSource();
            var pending = loader.GetAsync("https://images.invalid/cancel.png", cancel.Token); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await aborted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        var clock = new ManualPlaybackClock(); var calls = 0; var bytes = ImageBytes();
        using var retrying = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((_, _) => Task.FromResult(
            ++calls == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }))), clock);
        Assert.Null(await retrying.GetAsync("https://images.invalid/retry.png", CancellationToken.None));
        Assert.Null(await retrying.GetAsync("https://images.invalid/retry.png", CancellationToken.None)); Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.NotNull(await retrying.GetAsync("https://images.invalid/retry.png", CancellationToken.None)); Assert.Equal(2, calls);
    }

    [Fact]
    public async Task DecodedImageCacheHasByteBudgetAsWellAsEntryLimit()
    {
        var bytes = ImageBytes();
        using var loader = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }))));
        for (var i = 0; i < 40; i++) Assert.NotNull(await loader.GetAsync($"https://images.invalid/{i}.png", 480, 720, CancellationToken.None));
        Assert.InRange(loader.CachedBytes, 1, ChannelLogoLoader.MaxCacheBytes);
        Assert.InRange(loader.CachedCount, 1, 39);
    }
}
