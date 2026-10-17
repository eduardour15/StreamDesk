using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StreamDesk.Application;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class CatalogUiTests
{
    [Fact]
    public async Task WpfCatalogVirtualizesFiveThousandRowsFiltersAndSurvivesFailedRefresh()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stage = "starting";
        var thread = new Thread(() =>
        {
            System.Windows.Application? app = null;
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    // Load production styles without constructing App or starting production services.
                    app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources = new ResourceDictionary { Source = new Uri("/StreamDesk.Probe.Desktop;component/Resources/Theme.xaml", UriKind.Relative) };
                    await VerifyWindowAsync();
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    Volatile.Write(ref stage, "application shutdown");
                    app?.Shutdown();
                    Volatile.Write(ref stage, "dispatcher shutdown");
                    dispatcher.InvokeShutdown();
                    Volatile.Write(ref stage, "callback finished");
                }
            }));
            Dispatcher.Run();
            Volatile.Write(ref stage, "thread finished");
        }) { IsBackground = true, Name = "StreamDesk F2 WPF verification" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "WPF thread remained at: " + Volatile.Read(ref stage));
    }

    private static async Task VerifyWindowAsync()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        var fixture = CatalogFixtures.Create();
        await db.Store.ReplaceAsync("first", fixture); await db.Store.ReplaceAsync("second", CatalogFixtures.Create(20));
        db.Execute("INSERT INTO Favorites VALUES('first','live','000003','2026-10-17T12:00:00Z'); INSERT INTO Favorites VALUES('first','live','000004','2026-10-17T12:00:00Z');");
        var credentials = new FixtureCredentials();
        var logger = new FixtureLogger();
        var vm = new MainViewModel((_, _, _) => new FixtureProvider(fixture, failChannels: true), new FixturePlayback(),
            new ProfileService(db.Settings, credentials), logger, new CatalogService(db.Store), credentials);
        using var logos = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))));
        var window = new MainWindow(vm, logos)
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, Width = 1360, Height = 850
        };
        try
        {
            window.Show(); vm.CurrentTabIndex = 1;
            await WaitAsync(() => vm.Channels.Count == 5000 && !vm.IsCatalogLoading && !vm.IsBusy);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var list = (ListBox)window.FindName("ChannelsList");
            int Realized() => Enumerable.Range(0, list.Items.Count).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) is not null);
            var initialContainers = Realized();
            Assert.InRange(initialContainers, 1, 100);
            list.ScrollIntoView(list.Items[list.Items.Count - 1]);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1));
            var scrolledContainers = Realized(); Assert.InRange(scrolledContainers, 1, 100);

            var watch = Stopwatch.StartNew();
            vm.SearchText = "musica AGUILA";
            await WaitAsync(() => vm.Channels.Count == 1 && vm.Channels[0].Id == "004999");
            watch.Stop();
            vm.SelectedCategory = vm.Categories.Single(category => category.Id == "empty");
            await WaitAsync(() => vm.Channels.Count == 0); Assert.True(vm.HasNoChannels);
            vm.SelectedCategory = vm.Categories[0]; vm.SearchText = "";
            await WaitAsync(() => vm.Channels.Count == 5000);
            vm.FavoritesOnly = true; await WaitAsync(() => vm.Channels.Count == 2);
            vm.FavoritesOnly = false; await WaitAsync(() => vm.Channels.Count == 5000);

            await vm.LoadCatalogAsync();
            Assert.Equal(5000, vm.Channels.Count);
            Assert.Contains("Se conserva", vm.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("catalog_operation", logger.Events);

            vm.ActiveProfile = vm.Profiles.Single(profile => profile.Id == "second");
            vm.ActiveProfile = vm.Profiles.Single(profile => profile.Id == "first");
            await WaitAsync(() => !vm.IsCatalogLoading && vm.Channels.Count == 5000);

            var artifactRoot = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(artifactRoot.FullName, "StreamDesk.sln"))) artifactRoot = artifactRoot.Parent ?? throw new InvalidOperationException("Solution root missing.");
            var directory = Path.Combine(artifactRoot.FullName, "artifacts"); Directory.CreateDirectory(directory);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var canvas = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(directory, "f2-catalog.png"))) encoder.Save(file);
            await File.WriteAllTextAsync(Path.Combine(directory, "f2-ui-evidence.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                FixtureChannels = 5000, InitialRealizedRows = initialContainers, ScrolledRealizedRows = scrolledContainers,
                AccentSearchMilliseconds = watch.ElapsedMilliseconds, FailedRefreshPreservedChannels = vm.Channels.Count,
                ProfileSwitchPreservedIsolation = true, Source = "Automated WPF test with invented provider data"
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            await VerifyLogosAsync();
        }
        finally { window.Close(); }
    }

    private static async Task VerifyLogosAsync()
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 180, 120, 255 }, 4)));
        using var stream = new MemoryStream(); encoder.Save(stream); var png = stream.ToArray();
        var active = 0; var peak = 0; var requests = 0;
        using var loader = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler(async (_, token) =>
        {
            Interlocked.Increment(ref requests);
            var current = Interlocked.Increment(ref active); peak = Math.Max(peak, current);
            try { await Task.Delay(5, token); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) }; }
            finally { Interlocked.Decrement(ref active); }
        })));
        var images = await Task.WhenAll(Enumerable.Range(0, 150).Select(index => loader.GetAsync($"https://logos.invalid/{index}.png", CancellationToken.None)));
        Assert.All(images, image => { Assert.NotNull(image); Assert.True(image!.IsFrozen); });
        Assert.InRange(peak, 1, 4); Assert.Equal(128, loader.CachedCount);
        var previous = requests;
        Assert.Null(await loader.GetAsync("https://logos.invalid/a.png?token=fixture", CancellationToken.None));
        Assert.Equal(previous, requests);
        using var unsupported = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".webp", StringComparison.Ordinal))
            {
                // RIFF/WebP is intentionally unsupported by WPF's built-in WIC codecs.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' })
                });
            }
            var body = request.RequestUri.AbsolutePath.EndsWith(".svg", StringComparison.Ordinal)
                ? "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"
                : "<html>provider error</html>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, request.RequestUri.AbsolutePath.EndsWith(".svg", StringComparison.Ordinal) ? "image/svg+xml" : "text/html")
            });
        })));
        Assert.Null(await unsupported.GetAsync("https://logos.invalid/logo.svg", CancellationToken.None));
        Assert.Null(await unsupported.GetAsync("https://logos.invalid/logo.webp", CancellationToken.None));
        Assert.Null(await unsupported.GetAsync("https://logos.invalid/logo.png", CancellationToken.None));
        using var oversized = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[512 * 1024 + 1]) }))));
        Assert.Null(await oversized.GetAsync("https://logos.invalid/large.png", CancellationToken.None));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loader.GetAsync("https://logos.invalid/cancel.png", cancelled.Token));
    }

    private static async Task WaitAsync(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("WPF catalogue state did not settle.");
            await Task.Delay(10);
        }
    }
}
