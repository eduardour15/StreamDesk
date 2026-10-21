using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using StreamDesk.Probe.Infrastructure;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Playback;
using StreamDesk.Application;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

[Collection("NativePlayback")]
public sealed partial class CatalogUiTests
{
    private const int WindowStyleIndex = -16;
    private const int SystemMenuStyle = 0x00080000;
    private const int MinimizeBoxStyle = 0x00020000;
    private const int MaximizeBoxStyle = 0x00010000;
    private const int ResizeFrameStyle = 0x00040000;
    private const int CaptionStyle = 0x00C00000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetNativeWindowStyle(nint handle, int index);

    private static void AssertNativeCaptionButtons(Window window)
    {
        var style = GetNativeWindowStyle(new WindowInteropHelper(window).Handle, WindowStyleIndex);
        Assert.Equal(CaptionStyle, style & CaptionStyle);
        Assert.True((style & SystemMenuStyle) == SystemMenuStyle, $"Expected WS_SYSMENU; HWND style is 0x{style:X8}.");
        Assert.Equal(MinimizeBoxStyle, style & MinimizeBoxStyle);
        Assert.Equal(MaximizeBoxStyle, style & MaximizeBoxStyle);
        Assert.Equal(ResizeFrameStyle, style & ResizeFrameStyle);
    }

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
        // This WPF scenario also runs native playback and several UI regressions; allow headroom in the full suite.
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(150));
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
            vm.CategorySearch = "musica"; Assert.Contains(vm.VisibleCategories, category => category.Id == "music");
            vm.CategorySearch = "";
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
            await VerifyStaleCredentialReadDoesNotClearNewProfileAsync();
            await VerifyProfileChangeDoesNotUnlockProfileSaveAsync();
            await VerifyMvpUxAsync(directory, logos);
            await VerifyNativeControlPlacementAsync(logos);
            await VerifyContentUiAsync(directory, logos);
            await VerifyPictureInPictureAsync(directory, logos);
            await VerifyFluidityAsync(directory, logos);
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }
    }

    private static async Task VerifyContentUiAsync(string artifacts, ChannelLogoLoader logos)
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        var credentials = new FixtureCredentials(); var content = new SqliteContentStore(db.PathName, false);
        var backend = new ControlledBackend { Details = new(60000, 180000, true, [new(3, "Español"), new(4, "Original")], [new(-1, "Desactivados"), new(2, "Español")]) };
        var playback = new PlaybackService(backend);
        var vm = new MainViewModel((_, _, _) => new ContentFixtureProvider(), playback, new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials,
            new SqliteViewerStore(db.PathName, false), content, new XmlTvReader());
        var window = new MainWindow(vm, logos) { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            Assert.Equal(WindowStyle.SingleBorderWindow, window.WindowStyle);
            Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
            Assert.False(window.ExtendsContentIntoTitleBar);
            window.Show(); await WaitAsync(() => !vm.IsWorking && vm.ActiveProfile is not null);
            AssertNativeCaptionButtons(window);
            Assert.False(vm.HasMovies); Assert.False(vm.HasSeries);
            await vm.ProbeContentAsync(); Assert.True(vm.HasMovies); Assert.True(vm.HasSeries);
            var originalVideo = window.FindName("VideoView");
            vm.CurrentTabIndex = 5; Assert.True(vm.ShowMovieBrowse); Assert.False(vm.ShowPlayerPane); await WaitAsync(() => vm.MediaItems.Count == 5000);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("MediaBrowsePane")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("PlayerPane")).Visibility);
            vm.MediaCategorySearch = "ficcion"; Assert.Contains(vm.VisibleMediaCategories, category => category.Id == "fiction");
            vm.MediaCategory = vm.VisibleMediaCategories.Single(category => category.Id == "fiction");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var list = (ListBox)window.FindName("MediaList");
            var realized = Enumerable.Range(0, 5000).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) is not null);
            Assert.InRange(realized, 1, 100);
            vm.MediaSearch = "pelicula aguila"; await WaitAsync(() => vm.MediaItems.Count == 1);
            vm.SelectedMedia = vm.MediaItems[0]; Assert.True(vm.ShowMovieDetails); Assert.True(vm.ShowPlayerPane); await WaitAsync(() => vm.MediaDescription.Contains("Sin sinopsis", StringComparison.Ordinal));
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("MovieDetailsPane")).Visibility);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("PlayerPane")).Visibility);
            await vm.PlayMediaAsync();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            Assert.True(vm.IsImmersiveMode);
            Assert.Equal("Volver a películas", vm.ImmersiveReturnLabel);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("MovieDetailsPane")).Visibility);
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("Sidebar")).Visibility);
            var loadingIndicator = (FrameworkElement)window.FindName("VideoLoadingIndicator");
            Assert.True(vm.IsVideoLoading);
            Assert.True(vm.IsVideoLoadingIndeterminate);
            Assert.Equal(Visibility.Visible, loadingIndicator.Visibility);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Buffering, BufferPercent: 42));
            await WaitAsync(() => vm.HasVideoBufferPercent && vm.VideoBufferPercent == 42);
            Assert.Contains("42%", vm.VideoLoadingMessage, StringComparison.Ordinal);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await WaitAsync(() => vm.IsPlaying);
            Assert.False(vm.IsVideoLoading);
            Assert.Equal(Visibility.Collapsed, loadingIndicator.Visibility);
            Press(window, Key.Escape);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            Assert.False(vm.IsImmersiveMode);
            Assert.True(vm.IsPlaying);
            Assert.Equal(1, backend.Active);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("MovieDetailsPane")).Visibility);
            await vm.RefreshPlaybackDetailsAsync(); Assert.True(vm.CanSeek);
            await vm.SeekToAsync(70); Assert.Equal(70000, backend.Sought);
            vm.SelectedAudioTrack = vm.AudioTracks.Single(track => track.Id == 4); await vm.SelectTrackAsync(false); Assert.Equal((false, 4), backend.ChosenTrack);
            await vm.StopPlaybackAsync(); Assert.Equal(60000, Assert.Single(await content.LoadProgressAsync("first")).PositionMilliseconds);
            vm.CurrentTabIndex = 7; vm.SelectedProgress = Assert.Single(vm.ContinueWatching); await vm.PlayMediaAsync();
            Assert.Equal(60000, backend.Opens.Last().Options.StartPositionMilliseconds);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await WaitAsync(() => vm.IsPlaying);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Ended)); await WaitAsync(() => vm.ContinueWatching.Count == 0);
            vm.CurrentTabIndex = 6; Assert.True(vm.ShowSeriesBrowse); vm.MediaSearch = ""; vm.MediaCategory = null; vm.MediaCategorySearch = ""; await WaitAsync(() => vm.MediaItems.Count == 1 && vm.MediaItems[0].Kind == ContentKind.Series);
            vm.SelectedMedia = vm.MediaItems[0]; Assert.True(vm.ShowSeriesDetails); await WaitAsync(() => vm.Seasons.Count == 2);
            Assert.Equal("Episodio 1", vm.NowPlaying);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("SeriesDetailsPane")).Visibility);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var seriesBitmap = new RenderTargetBitmap(1360, 850, 96, 96, PixelFormats.Pbgra32); seriesBitmap.Render((Visual)window.Content);
            var seriesPng = new PngBitmapEncoder(); seriesPng.Frames.Add(BitmapFrame.Create(seriesBitmap)); using (var file = File.Create(Path.Combine(artifacts, "f5-series-details.png"))) seriesPng.Save(file);
            vm.Season = vm.Seasons[1]; Assert.Empty(vm.Episodes); vm.Season = vm.Seasons[0]; Assert.Equal(2, vm.Episodes.Count);
            Assert.Equal("2 temporadas · 2 episodios", vm.SeriesSummary);
            vm.SelectedEpisode = vm.Episodes[0]; await vm.PlayMediaAsync(); await vm.NavigateEpisodeAsync(1); Assert.Equal("episode-2", vm.SelectedEpisode?.Id);
            Assert.Equal(1, backend.Peak); Assert.Same(originalVideo, window.FindName("VideoView"));
            await vm.StopPlaybackAsync();
            vm.CurrentTabIndex = 1; await vm.LoadCatalogAsync(); vm.SelectedChannel = vm.Channels[0]; await vm.RefreshEpgAsync();
            Assert.Contains("Programa actual", vm.EpgNow, StringComparison.Ordinal); Assert.Contains("Programa siguiente", vm.EpgNext, StringComparison.Ordinal);
            vm.CurrentTabIndex = 5; vm.MediaSearch = ""; await WaitAsync(() => vm.MediaItems.Count == 5000);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1360, 850, 96, 96, PixelFormats.Pbgra32); bitmap.Render((Visual)window.Content);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(artifacts, "f5-content.png"))) png.Save(file);
            vm.ActiveProfile = vm.Profiles.Single(profile => profile.Id == "second"); await WaitAsync(() => !vm.IsWorking);
            Assert.False(vm.HasMovies); Assert.False(vm.HasSeries); Assert.Empty(vm.ContinueWatching);
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }
    }

    private sealed class ContentFixtureProvider : IIptvProvider, IContentProvider
    {
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<XtreamCategory>>([]);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<XtreamChannel>>([new("42", "Canal de prueba", null, "live", "ts", EpgChannelId: "fixture.epg")]);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new("https://provider.invalid/fixture.ts");
        public Uri CreateMediaUri(MediaItem item) => new("https://provider.invalid/fixture.mp4");
        public Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default) => Task.FromResult(new MediaCatalog([new("fiction", "Ficción")],
            kind == ContentKind.Movie ? Enumerable.Range(0, 5000).Select(i => new MediaItem(i.ToString(), i == 4999 ? "Película Águila" : $"Película {i}", kind, "fiction", "mp4")).ToArray() : [new("series-1", "Serie de prueba", kind)], DateTimeOffset.UtcNow));
        public Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default) => Task.FromResult(movie with { Plot = "" });
        public Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default) => Task.FromResult(new MediaCatalog([new("1", "Temporada 1"), new("2", "Temporada vacía")],
            [new("episode-1", "Episodio 1", ContentKind.Episode, "1", "mp4", SeriesId: seriesId, Season: 1, Episode: 1), new("episode-2", "Episodio 2", ContentKind.Episode, "1", "mp4", SeriesId: seriesId, Season: 1, Episode: 2)], DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EpgProgram>>(
            [new("Programa actual", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(20)), new("Programa siguiente", DateTimeOffset.UtcNow.AddMinutes(20), DateTimeOffset.UtcNow.AddMinutes(50))]);
        public void Dispose() { }
    }

    private static async Task VerifyNativeControlPlacementAsync(ChannelLogoLoader logos)
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync();
        var imagePath = Path.Combine(db.Root, "local-fixture.bmp");
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgr24, null, Enumerable.Repeat((byte)180, 16 * 16 * 3).ToArray(), 16 * 3)));
        using (var file = File.Create(imagePath)) encoder.Save(file);
        var credentials = new FixtureCredentials(); var nativeBackend = new LibVlcBackend(); var playback = new PlaybackService(nativeBackend);
        var vm = new MainViewModel((_, _, _) => new LocalFixtureProvider(new Uri(imagePath)), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        var window = new MainWindow(vm, logos) { Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        try
        {
            window.Show(); await WaitAsync(() => vm.IsPlaybackReady && !vm.IsWorking);
            vm.CurrentTabIndex = 1; await vm.ConnectAsync("https://provider.invalid", "fixture-user", "fixture-password");
            await vm.LoadCatalogAsync(); vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync();
            await WaitAsync(() => vm.IsPlaying);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var video = (LibVLCSharp.WPF.VideoView)window.FindName("VideoView");
            var player = Assert.IsType<LibVLCSharp.Shared.MediaPlayer>(vm.PlayerHandle);
            Assert.NotEqual(nint.Zero, player.Hwnd);
            var controls = (FrameworkElement)window.FindName("PlayerControls");
            var videoBounds = video.TransformToAncestor(window).TransformBounds(new Rect(video.RenderSize));
            var controlsBounds = controls.TransformToAncestor(window).TransformBounds(new Rect(controls.RenderSize));
            Assert.False(videoBounds.IntersectsWith(controlsBounds));
            Assert.NotNull(window.InputHitTest(controlsBounds.TopLeft + new Vector(12, 12)));
            var loadingBanner = (FrameworkElement)window.FindName("VideoLoadingIndicator");
            Assert.Equal(Visibility.Collapsed, loadingBanner.Visibility);
            vm.AmplificationDb = 4;
            await WaitAsync(() => nativeBackend.IsEqualizerEnabled && Math.Abs(nativeBackend.ActivePreampDb - 4f) < 0.05f);
            vm.AmplificationDb = 25;
            await WaitAsync(() => nativeBackend.ActiveAmplificationDb == 12 && Math.Abs(nativeBackend.ActivePreampDb - 12f) < 0.05f);
            vm.AmplificationDb = 0;
            await WaitAsync(() => !nativeBackend.IsEqualizerEnabled && nativeBackend.ActiveAmplificationDb == 0);
            await VerifyNativePictureInPictureAsync(window, vm, player, db.Root);
            var stop = Descendants<ButtonBase>(controls).Single(button => button.IsVisible && button.Content as string == "⏹");
            Assert.Equal("Detener", System.Windows.Automation.AutomationProperties.GetName(stop));
            stop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitAsync(() => !playback.HasActiveMedia);
            await vm.PlaySelectedAsync(); await WaitAsync(() => vm.IsPlaying);
            Click(window, "PipButton"); await SettleAsync(window); AssertCompactControls(window);
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }
        Assert.False(playback.HasActiveMedia);
    }

    private sealed class LocalFixtureProvider(Uri source) : IIptvProvider
    {
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<XtreamCategory>>([]);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<XtreamChannel>>([new("fixture", "Imagen local de prueba", null, "live", "ts")]);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => source;
        public void Dispose() { }
    }

    private static async Task VerifyMvpUxAsync(string artifacts, ChannelLogoLoader logos)
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync();
        var credentials = new FixtureCredentials(); var store = new SqliteViewerStore(db.PathName, false);
        var calls = 0; var backend = new ControlledBackend(); var playback = new PlaybackService(backend);
        MainViewModel Create(PlaybackService service) => new((_, _, _) => { calls++; return new FixtureProvider(CatalogFixtures.Create(50)); }, service,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials, store);
        var vm = Create(playback);
        var window = new MainWindow(vm, logos) { Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        try
        {
            window.Show(); await WaitAsync(() => !vm.IsWorking);
            Assert.False(vm.AutoPlayLast); Assert.Equal(0, calls);
            vm.ProfileName = "Cuenta de prueba"; vm.ProfileBaseUrl = "https://provider.invalid"; vm.ProfileUsername = "fixture-user";
            await vm.CreateProfileAsync("fixture-password"); await WaitAsync(() => !vm.IsWorking && vm.ActiveProfile is not null);
            vm.CurrentTabIndex = 1; await vm.LoadCatalogAsync(); Assert.Equal(50, vm.Channels.Count);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var category = (ComboBox)window.FindName("CategoryFilter");
            Assert.Contains(Descendants<TextBlock>(category), text => text.Text == "Todas las categorías");
            Assert.DoesNotContain(Descendants<TextBlock>(category), text => text.Text.Contains("XtreamCategory", StringComparison.Ordinal));
            foreach (var background in new[] { "CanvasBrush", "SurfaceBrush", "SurfaceRaisedBrush" })
                foreach (var foreground in new[] { "TextPrimaryBrush", "TextMutedBrush", "AccentBrush" })
                    Assert.True(Contrast(((SolidColorBrush)window.FindResource(foreground)).Color, ((SolidColorBrush)window.FindResource(background)).Color) >= 4.5);
            var video = window.FindName("VideoView");
            vm.SearchText = "repetido"; await WaitAsync(() => vm.Channels.Count == 2);
            vm.SelectedChannel = vm.Channels[0];
            await vm.ToggleFavoriteAsync(); Assert.True(vm.SelectedIsFavorite);
            vm.CurrentTabIndex = 2; await WaitAsync(() => vm.Channels.Count == 1);
            Assert.Same(video, window.FindName("VideoView"));
            vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync();
            Assert.Empty((await store.LoadAsync(vm.ActiveProfile!.Id)).Recents);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await WaitAsync(() => vm.HasLastChannel);
            Assert.Equal(vm.SelectedChannel!.Id, vm.LastChannel!.Id);
            vm.CurrentTabIndex = 3; await WaitAsync(() => vm.Channels.Count == 1);
            Assert.Same(video, window.FindName("VideoView"));
            var search = (TextBox)window.FindName("SearchBox"); var muted = vm.IsMuted;
            search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.M) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.Equal(muted, vm.IsMuted);
            var controls = (FrameworkElement)window.FindName("PlayerControls");
            await WaitAsync(() => controls.Visibility == Visibility.Hidden);
            ((UIElement)window.FindName("PlayerPane")).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
            Assert.Equal(Visibility.Visible, controls.Visibility);
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Forbidden));
            await WaitAsync(() => vm.CanRetryPlayback && controls.Visibility == Visibility.Visible);
            Assert.Single((await store.LoadAsync(vm.ActiveProfile.Id)).Recents);
            vm.SearchText = "sin coincidencias"; await WaitAsync(() => vm.Channels.Count == 0);
            Assert.Null(vm.SelectedChannel);
            var opens = backend.Opens.Count;
            await vm.RetryPlaybackAsync(); Assert.Equal(opens + 1, backend.Opens.Count);
            await vm.StopPlaybackAsync();

            vm.CurrentTabIndex = 1; await WaitAsync(() => vm.Channels.Count == 50);
            var shell = (FrameworkElement)window.FindName("Shell");
            var scales = new[] { 1d, 1.25, 1.5, 2d };
            foreach (var scale in scales)
            {
                window.Width = 760 * scale; window.Height = 600 * scale; shell.LayoutTransform = new ScaleTransform(scale, scale);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                var player = (FrameworkElement)window.FindName("PlayerPane");
                Assert.True(player.ActualWidth > 180); Assert.True(player.ActualHeight > 100);
                var bounds = controls.TransformToAncestor(shell).TransformBounds(new Rect(controls.RenderSize));
                Assert.True(bounds.Right <= shell.ActualWidth + 2 && bounds.Bottom <= shell.ActualHeight + 2, $"Controls exceed available layout space: scale={scale}, bounds={bounds}, shell={shell.RenderSize}.");
            }
            shell.LayoutTransform = Transform.Identity; window.Width = 1360; window.Height = 850;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1360, 850, 96, 96, PixelFormats.Pbgra32); bitmap.Render((Visual)window.Content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(artifacts, "f4-mvp.png"))) encoder.Save(file);
            await File.WriteAllTextAsync(Path.Combine(artifacts, "f4-ui-evidence.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Source = "WPF real con datos y backend ficticios", Flow = "Crear perfil, buscar, reproducir, favorito, reciente, reiniciar",
                LayoutScales = scales, PhysicalDpiVerified = false, Autohide = true, EditingShortcutIgnored = true, SharedVideoView = true
            }));
        }
        finally { window.Close(); await WaitAsync(() => !window.IsVisible); }

        var restartedBackend = new ControlledBackend(); var restarted = Create(new PlaybackService(restartedBackend));
        await restarted.LoadProfilesAsync();
        Assert.True(restarted.HasLastChannel); Assert.Single(restarted.FavoriteChannelIds);
        Assert.False(restarted.AutoPlayLast); Assert.Empty(restartedBackend.Opens);
        await restarted.SetAutoPlayAsync(true); await restarted.DisposeAsync();
        var autoBackend = new ControlledBackend(); await using var auto = Create(new PlaybackService(autoBackend));
        await auto.LoadProfilesAsync(); Assert.Single(autoBackend.Opens);
        await auto.LoadProfilesAsync(); Assert.Single(autoBackend.Opens);
        await auto.SuspendPlaybackAsync(); Assert.Equal(0, autoBackend.Active);
        await db.AddProfileAsync("second"); await auto.LoadProfilesAsync();
        auto.ActiveProfile = auto.Profiles.Single(profile => profile.Id == "second");
        await WaitAsync(() => !auto.IsWorking); Assert.Single(autoBackend.Opens);
        Assert.Empty(auto.FavoriteChannelIds); Assert.False(auto.HasLastChannel);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static double Contrast(Color foreground, Color background)
    {
        static double Channel(byte value) { var c = value / 255d; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        var a = Luminance(foreground); var b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
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
        Assert.InRange(peak, 1, 4); Assert.Equal(150, loader.CachedCount); Assert.Equal(500, ChannelLogoLoader.MaxCacheEntries);
        var previous = requests;
        Assert.Null(await loader.GetAsync("https://logos.invalid/a.png?token=fixture", CancellationToken.None));
        Assert.Equal(previous, requests);
        var cdnLogo = await loader.GetAsync("https://logos.invalid/poster.png?w=240&h=360&sig=cdn123", 240, 360, CancellationToken.None);
        Assert.NotNull(cdnLogo); Assert.True(cdnLogo!.IsFrozen);
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

    private static async Task VerifyStaleCredentialReadDoesNotClearNewProfileAsync()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        await db.Store.ReplaceAsync("second", CatalogFixtures.Create(20));
        var first = (await db.Settings.GetProfilesAsync()).Single(profile => profile.Id == "first");
        var credentials = new DeferredCredentials(first.CredentialKey);
        var providerCalls = 0;
        using var vm = new MainViewModel((_, _, _) => { providerCalls++; return new FixtureProvider(CatalogFixtures.Create()); }, new FixturePlayback(),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        await vm.LoadProfilesAsync();
        await WaitAsync(() => !vm.IsWorking && vm.Channels.Count == 10);
        var pending = vm.ConnectProfileAsync();
        await credentials.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.ActiveProfile = vm.Profiles.Single(profile => profile.Id == "second");
        await WaitAsync(() => !vm.IsWorking && vm.Channels.Count == 20);
        await vm.ConnectProfileAsync();
        var summary = vm.ConnectionSummary; Assert.NotNull(summary);
        credentials.Release.SetResult();
        await pending;
        Assert.Equal(summary, vm.ConnectionSummary);
        Assert.Equal("ts", vm.SelectedFormat);
        Assert.Equal(1, providerCalls);
        Assert.Equal(20, vm.Channels.Count);
    }

    private static async Task VerifyProfileChangeDoesNotUnlockProfileSaveAsync()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        var credentials = new DeferredCredentials(null);
        using var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create()), new FixturePlayback(),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        await vm.LoadProfilesAsync(); await WaitAsync(() => !vm.IsWorking);
        vm.ProfileName = "New fixture"; vm.ProfileBaseUrl = "https://provider.invalid"; vm.ProfileUsername = "fixture-user";
        var pending = vm.CreateProfileAsync("fixture-password");
        await credentials.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            vm.ActiveProfile = vm.Profiles.Single(profile => profile.Id == "second");
            await WaitAsync(() => !vm.IsCatalogLoading);
            Assert.True(vm.IsBusy);
            await vm.CreateProfileAsync("fixture-password");
            Assert.Equal(1, credentials.Saves);
        }
        finally { credentials.Release.SetResult(); await pending; }
    }

    private sealed class DeferredCredentials(string? delayedKey) : ICredentialStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Saves { get; private set; }
        public async Task<StreamDesk.Probe.Core.StoredCredentials?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (key == delayedKey) { Entered.TrySetResult(); await Release.Task; } // Simulate an OS store finishing after cancellation.
            return new("fixture-user", "fixture-password");
        }
        public async Task SaveAsync(string key, StreamDesk.Probe.Core.StoredCredentials value, CancellationToken cancellationToken = default)
        { Saves++; Entered.TrySetResult(); await Release.Task; }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
