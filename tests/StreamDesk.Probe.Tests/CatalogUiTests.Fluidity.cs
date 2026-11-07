using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Tests;

public sealed partial class CatalogUiTests
{
    private static async Task VerifyFluidityAsync(string directory, StreamDesk.Probe.Desktop.ChannelLogoLoader logos)
    {
        await VerifyFluidityStatesAsync(directory, logos);
        await VerifyRedesignAndPerformanceAsync(directory, logos);
        var measurements = new List<object>();
        foreach (var count in new[] { 5000, 20000 })
        {
            using var db = new TestDatabase();
            await db.AddProfileAsync("first");
            await db.Store.ReplaceAsync("first", CatalogFixtures.Create(count));
            var credentials = new FixtureCredentials();
            var provider = new LargeMediaProvider(count);
            var vm = new StreamDesk.Probe.Desktop.MainViewModel((_, _, _) => provider, new FixturePlayback(),
                new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
            var window = new StreamDesk.Probe.Desktop.MainWindow(vm, logos)
            {
                Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, Width = 1360, Height = 850
            };
            var delays = new List<double>();
            var lastTick = Stopwatch.GetTimestamp();
            var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            heartbeat.Tick += (_, _) => { var now = Stopwatch.GetTimestamp(); delays.Add(Math.Max(0, Stopwatch.GetElapsedTime(lastTick, now).TotalMilliseconds - 16)); lastTick = now; };
            try
            {
                window.Show();
                await WaitAsync(() => vm.Channels.Count == count && !vm.IsWorking);
                await vm.ProbeContentAsync();
                vm.CurrentTabIndex = 5;
                await WaitAsync(() => vm.MediaItems.Count == count);
                var list = (ListBox)window.FindName("MediaGridList");
                Assert.True(vm.IsMediaGridView);
                var searchBox = (TextBox)window.FindName("MediaSearchBox");
                var filterTimes = new List<double>();
                var visibleTimes = new List<double>();
                var allocationStart = GC.GetTotalAllocatedBytes();
                using var process = Process.GetCurrentProcess();
                var cpuStart = process.TotalProcessorTime;
                var watch = Stopwatch.StartNew();
                heartbeat.Start();
                for (var sample = 0; sample < 8; sample++)
                {
                    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
                    { if (args.PropertyName == nameof(vm.MediaItems)) ready.TrySetResult(); }
                    vm.PropertyChanged += Changed;
                    var queryStart = Stopwatch.GetTimestamp();
                    try
                    {
                        searchBox.Text = sample % 2 == 0 ? "pelicula aguila" : "";
                        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        filterTimes.Add(Stopwatch.GetElapsedTime(queryStart).TotalMilliseconds);
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                        window.UpdateLayout();
                        visibleTimes.Add(Stopwatch.GetElapsedTime(queryStart).TotalMilliseconds);
                        Assert.Equal(sample % 2 == 0 ? count / 2 : count, vm.MediaItems.Count);
                    }
                    finally { vm.PropertyChanged -= Changed; }
                }
                heartbeat.Stop();
                var realized = Enumerable.Range(0, list.Items.Count).Count(i => list.ItemContainerGenerator.ContainerFromIndex(i) is not null);
                Assert.InRange(realized, 1, 100);
                process.Refresh();
                double Percentile(List<double> values, double p) => values.Order().ElementAt(Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * p) - 1));
                measurements.Add(new
                {
                    Count = count, Samples = visibleTimes.Count, DebounceMilliseconds = 120,
                    FilterIncludingDebounceP50 = Percentile(filterTimes, .5), FilterIncludingDebounceP95 = Percentile(filterTimes, .95),
                    VisibleP50 = Percentile(visibleTimes, .5), VisibleP95 = Percentile(visibleTimes, .95),
                    DispatcherDelayP95 = Percentile(delays, .95), RealizedRows = realized,
                    AllocatedBytes = GC.GetTotalAllocatedBytes() - allocationStart, PrivateBytes = process.PrivateMemorySize64,
                    CpuMilliseconds = (process.TotalProcessorTime - cpuStart).TotalMilliseconds, ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds
                });
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1360, 850, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render((Visual)window.Content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var image = File.Create(Path.Combine(directory, $"flux-media-{count}.png"));
                encoder.Save(image);
            }
            finally { heartbeat.Stop(); window.Close(); await WaitAsync(() => !window.IsVisible); }
        }
        var label = Environment.GetEnvironmentVariable("STREAMDESK_FLUX_RUN") == "before" ? "before" : "after";
        await File.WriteAllTextAsync(Path.Combine(directory, $"flux-search-{label}.json"), JsonSerializer.Serialize(new
        {
            Source = "WPF fixture; no IPTV; 8 alternating queries per catalog; warm cache; includes configured 120ms debounce",
            Runtime = Environment.Version.ToString(), Processors = Environment.ProcessorCount, Measurements = measurements
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task VerifyFluidityStatesAsync(string directory, StreamDesk.Probe.Desktop.ChannelLogoLoader logos)
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials(); var backend = new ControlledBackend();
        var vm = new StreamDesk.Probe.Desktop.MainViewModel((_, _, _) => new LargeMediaProvider(20), new PlaybackService(backend),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials, settings: db.Settings);
        var window = new StreamDesk.Probe.Desktop.MainWindow(vm, logos) { Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show(); await WaitAsync(() => !vm.IsWorking && vm.Profiles.Count == 1);
            vm.CurrentTabIndex = 1; await vm.LoadCatalogAsync();
            vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync(); await SettleAsync(window);
            var video = (FrameworkElement)window.FindName("VideoView");
            var initialBounds = video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize));
            var indicator = (FrameworkElement)window.FindName("VideoLoadingIndicator");
            Assert.Equal(Visibility.Visible, indicator.Visibility);
            Assert.True(initialBounds.Contains(BoundsIn(indicator, window)));
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await WaitAsync(() => vm.IsPlaying); await SettleAsync(window);
            Assert.Equal(initialBounds, video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize)));
            var controls = (FrameworkElement)window.FindName("PlayerControls");
            controls.Visibility = Visibility.Collapsed; await SettleAsync(window);
            Assert.Equal(initialBounds, video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize)));
            controls.Visibility = Visibility.Visible;
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Buffering, BufferPercent: 35)); await WaitAsync(() => vm.IsVideoLoading); await SettleAsync(window);
            Assert.Equal(initialBounds, video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize)));
            var title = (FrameworkElement)window.FindName("ControlsMediaTitle");
            Assert.True(title.ActualWidth > controls.ActualWidth - 40);
            void Capture(string name)
            {
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render((Visual)window.Content); var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var image = File.Create(Path.Combine(directory, name)); encoder.Save(image);
            }
            Capture("flux-loading.png");
            vm.SelectedTheme = "Light"; await SettleAsync(window);
            Assert.Equal(Color.FromRgb(243, 243, 243), ((SolidColorBrush)window.Background).Color);
            Assert.Equal(Color.FromRgb(26, 26, 26), ((SolidColorBrush)((Wpf.Ui.Controls.TitleBar)window.FindName("AppTitleBar")).ButtonsForeground).Color);
            var search = (TextBox)window.FindName("SearchBox");
            Assert.Equal(Color.FromRgb(26, 26, 26), ((SolidColorBrush)search.Foreground).Color);
            Assert.Equal(Color.FromRgb(235, 235, 235), ((SolidColorBrush)search.Background).Color);
            Capture("flux-light.png"); vm.SelectedTheme = "Dark";
            await vm.StopPlaybackAsync(); await vm.ProbeContentAsync(); vm.CurrentTabIndex = 5;
            await WaitAsync(() => vm.MediaItems.Count == 20 && !vm.IsMediaFiltering);
            var combo = (ComboBox)window.FindName("MediaCategoryFilter"); combo.IsDropDownOpen = true; await SettleAsync(window);
            var categorySearch = (TextBox)combo.Template.FindName("MediaCategoryFilterSearchBox", combo);
            categorySearch.Text = "ficcion";
            Assert.Contains(vm.VisibleMediaCategories, c => c.Id == "fiction");
            categorySearch.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(window), 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            Assert.False(combo.IsDropDownOpen);
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }
    }

    private static async Task VerifyRedesignAndPerformanceAsync(string directory, StreamDesk.Probe.Desktop.ChannelLogoLoader logos)
    {
        using var db = new TestDatabase();
        await db.AddProfileAsync("first");
        var snapshot = CatalogFixtures.Create(500);
        await db.Store.ReplaceAsync("first", snapshot);
        db.Execute("INSERT INTO Favorites VALUES('first','live','000001','2026-10-17T12:00:00Z'); INSERT INTO Favorites VALUES('first','live','000002','2026-10-17T12:00:00Z'); INSERT INTO Favorites VALUES('first','live','000003','2026-10-17T12:00:00Z');");
        db.Execute("INSERT INTO PlaybackHistory VALUES('first','live','000002','2026-10-17T12:05:00Z');");

        var contentStore = new StreamDesk.Probe.Infrastructure.SqliteContentStore(db.PathName, false);
        var movies = Enumerable.Range(1, 24).Select(i => new MediaItem(
            $"m-{i}",
            $"Horizonte Galáctico ({2020 + (i % 6)}) #{i}",
            ContentKind.Movie,
            "fiction",
            "mp4",
            Plot: "Una expedición interestelar descubre una señal ancestral en los confines del sistema exterior mientras la tripulación lucha por regresar.")).ToArray();
        var series = Enumerable.Range(1, 24).Select(i => new MediaItem(
            $"s-{i}",
            $"Crónicas del Norte ({2019 + (i % 6)}) #{i}",
            ContentKind.Series,
            "fiction",
            Plot: "Drama de misterio ambientado en una estación polar aislada durante el invierno ártico.")).ToArray();
        await contentStore.SaveCatalogAsync("first", ContentKind.Movie, "", new MediaCatalog([new("fiction", "Ciencia Ficción")], movies, DateTimeOffset.UtcNow));
        await contentStore.SaveCatalogAsync("first", ContentKind.Series, "", new MediaCatalog([new("fiction", "Drama")], series, DateTimeOffset.UtcNow));
        await contentStore.SaveProgressAsync("first", new MediaProgress(movies[0], 3600_000, 7200_000, DateTimeOffset.UtcNow));
        await contentStore.SaveProgressAsync("first", new MediaProgress(new MediaItem("ep-104", "Crónicas del Norte", ContentKind.Episode, "1", "mp4", SeriesId: "s-1", Season: 1, Episode: 4), 1500_000, 3000_000, DateTimeOffset.UtcNow.AddMinutes(-5)));

        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var viewerStore = new StreamDesk.Probe.Infrastructure.SqliteViewerStore(db.PathName, false);
        var startupSw = Stopwatch.StartNew();
        var vm = new StreamDesk.Probe.Desktop.MainViewModel(
            (_, _, _) => new LargeMediaProvider(500),
            new PlaybackService(backend),
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            viewerStore,
            contentStore,
            settings: db.Settings);
        var window = new StreamDesk.Probe.Desktop.MainWindow(vm, logos)
        {
            Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, Width = 1366, Height = 768
        };
        try
        {
            window.Show();
            await WaitAsync(() => vm.Channels.Count == 500 && vm.HasMovies && vm.HasSeries && !vm.IsWorking);
            startupSw.Stop();

            void SaveShot(string fileName)
            {
                var w = (int)window.ActualWidth;
                var h = (int)window.ActualHeight;
                var rootVisual = (Visual)window.Content;
                var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(rootVisual);
                if (window.FindName("VideoSurface") is FrameworkElement videoSurface &&
                    videoSurface.IsVisible &&
                    videoSurface.ActualWidth > 0 &&
                    videoSurface.ActualHeight > 0 &&
                    window.FindName("PlayerOverlay") is FrameworkElement overlay)
                {
                    var bounds = videoSurface.TransformToAncestor(rootVisual).TransformBounds(new Rect(videoSurface.RenderSize));
                    overlay.Measure(bounds.Size);
                    overlay.Arrange(new Rect(bounds.Size));
                    overlay.UpdateLayout();
                    var overlayBmp = new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1, (int)bounds.Width), Math.Max(1, (int)bounds.Height), 96, 96, PixelFormats.Pbgra32);
                    overlayBmp.Render(overlay);
                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        dc.DrawImage(bmp, new Rect(0, 0, w, h));
                        dc.DrawImage(overlayBmp, bounds);
                    }
                    var combined = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    combined.Render(dv);
                    bmp = combined;
                }
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                using var stream = File.Create(Path.Combine(directory, fileName));
                enc.Save(stream);
            }

            // 1. Verify Home (Inicio) structure & data
            vm.CurrentTabIndex = 0;
            await SettleAsync(window);
            Assert.True(vm.HasHomeFeatured);
            Assert.True(vm.HasHomeFeaturedYear);
            Assert.Equal("2021", vm.HomeFeaturedYear);
            Assert.True(vm.ShowHomeResumeChannel);
            Assert.Contains("Último canal:", vm.HomeResumeChannelLabel);
            Assert.True(vm.HasContinueWatching);
            Assert.True(((FrameworkElement)window.FindName("HomeContinueScroll")).IsVisible);
            Assert.True(vm.HasHomeFavoriteChannels);
            Assert.True(vm.HasHomeMovies);
            Assert.True(vm.HasHomeSeries);
            Assert.True(vm.MediaProgressMap.ContainsKey(movies[0].StorageKey));

            // Measure cached property reads on Home (1,000 iterations)
            var cacheAllocStart = GC.GetTotalAllocatedBytes(true);
            var cacheSw = Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++)
            {
                _ = vm.HomeMovies;
                _ = vm.HomeSeries;
                _ = vm.HomeContinueWatching;
                _ = vm.HomeFavoriteChannels;
                _ = vm.HomeFeaturedMedia;
                _ = vm.MediaProgressMap;
            }
            cacheSw.Stop();
            var cacheAllocatedBytes = GC.GetTotalAllocatedBytes(true) - cacheAllocStart;
            Assert.True(cacheSw.Elapsed.TotalMilliseconds < 25, $"Cached Home property reads took {cacheSw.Elapsed.TotalMilliseconds:F2}ms");

            // Capture Home at 1366x768, 1600x900, 1920x1080
            foreach (var (w, h) in new[] { (1366, 768), (1600, 900), (1920, 1080) })
            {
                window.Width = w; window.Height = h;
                await SettleAsync(window);
                SaveShot($"redesign-home-{w}x{h}.png");
            }

            // 2. Verify Live TV (TV en vivo) idle state, channel list permanence, favorite stability, and playback states
            var navSw = Stopwatch.StartNew();
            vm.CurrentTabIndex = 1;
            await SettleAsync(window);
            navSw.Stop();

            window.Width = 1366; window.Height = 768;
            await SettleAsync(window);
            var catalogPane = (FrameworkElement)window.FindName("CatalogPane");
            var playerChrome = (FrameworkElement)window.FindName("PlayerChrome");
            Assert.Equal(Visibility.Visible, catalogPane.Visibility);
            Assert.Equal("Selecciona un canal", vm.PlayerEmptyMessage);
            Assert.Equal(0, playerChrome.Opacity);
            SaveShot("redesign-livetv-idle-1366x768.png");

            // Verify favorite toggle does NOT replace Channels list reference or change SelectedChannel
            var channelsRefBefore = vm.Channels;
            vm.SelectedChannel = vm.Channels[0];
            await vm.ToggleFavoriteAsync(vm.Channels[5]);
            Assert.Same(channelsRefBefore, vm.Channels);
            Assert.Equal(vm.Channels[0].Id, vm.SelectedChannel?.Id);

            // Verify playing a channel keeps CatalogPane visible and does NOT replace Channels list reference
            var zappingSw = Stopwatch.StartNew();
            await vm.PlaySelectedAsync();
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await WaitAsync(() => vm.IsPlaying);
            await SettleAsync(window);
            zappingSw.Stop();

            Assert.Equal(Visibility.Visible, catalogPane.Visibility);
            Assert.Same(channelsRefBefore, vm.Channels);
            Assert.True(vm.IsLivePlayback);
            Assert.Equal(1, playerChrome.Opacity);

            foreach (var (w, h) in new[] { (1366, 768), (1600, 900), (1920, 1080) })
            {
                window.Width = w; window.Height = h;
                await SettleAsync(window);
                Assert.Equal(Visibility.Visible, catalogPane.Visibility);
                SaveShot($"redesign-livetv-playing-{w}x{h}.png");
            }

            // Verify No Signal / Error state with Retry button
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.NotFound));
            await WaitAsync(() => vm.CanRetryPlayback && vm.IsNoSignalState);
            await SettleAsync(window);
            Assert.Equal("Sin señal disponible", vm.PlayerEmptyMessage);
            Assert.True(vm.ShowPlaybackPlaceholder);
            SaveShot("redesign-livetv-error-1920x1080.png");

            await File.WriteAllTextAsync(Path.Combine(directory, "redesign-performance-evidence.json"), JsonSerializer.Serialize(new
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                StartupAndCacheHydrationMs = Math.Round(startupSw.Elapsed.TotalMilliseconds, 2),
                HomeToLiveTvNavigationMs = Math.Round(navSw.Elapsed.TotalMilliseconds, 2),
                HomeCachedReads1000IterationsMs = Math.Round(cacheSw.Elapsed.TotalMilliseconds, 3),
                HomeCachedReadsAllocatedBytes = cacheAllocatedBytes,
                ChannelPlayAndLayoutSettleMs = Math.Round(zappingSw.Elapsed.TotalMilliseconds, 2),
                ChannelsReferencePreservedOnFavoriteAndPlay = true,
                LiveTvCatalogVisibleDuringPlayback = true,
                ResolutionsVerified = new[] { "1366x768", "1600x900", "1920x1080" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }
    }
}

internal sealed class LargeMediaProvider(int count) : IIptvProvider, IContentProvider
{
    public bool UsesUnencryptedTransport => false;
    public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts"]));
    public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(CatalogFixtures.Create(0).Categories);
    public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CatalogFixtures.Create(count).Channels);
    public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new("https://provider.invalid/live.ts");
    public Uri CreateMediaUri(MediaItem item) => new("https://provider.invalid/movie.mp4");
    public Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default) => Task.FromResult(new MediaCatalog(
        [new("fiction", "Ficción"), new("empty", "Vacía")], Enumerable.Range(0, count).Select(i => new MediaItem(i.ToString(), i % 2 == 0 ? $"Película Águila {i:D5}" : $"Título Música {i:D5}", kind, "fiction", "mp4")).ToArray(), DateTimeOffset.UtcNow));
    public Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default) => Task.FromResult(movie);
    public Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default) => Task.FromResult(new MediaCatalog([], [], DateTimeOffset.UtcNow));
    public Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EpgProgram>>([]);
    public void Dispose() { }
}
