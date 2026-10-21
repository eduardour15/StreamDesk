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
                var list = (ListBox)window.FindName("MediaList");
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
            Assert.False(initialBounds.IntersectsWith(indicator.TransformToAncestor(window).TransformBounds(new Rect(indicator.RenderSize))));
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await WaitAsync(() => vm.IsPlaying); await SettleAsync(window);
            Assert.Equal(initialBounds, video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize)));
            var controls = (FrameworkElement)window.FindName("PlayerControls");
            controls.Visibility = Visibility.Hidden; await SettleAsync(window);
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
