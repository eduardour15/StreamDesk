using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed partial class CatalogUiTests
{
    private static async Task VerifyContinuityPreferencesAsync(string artifacts, ChannelLogoLoader logos)
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync();
        var backend = new ControlledBackend(); var clock = new ManualPlaybackClock(); var playback = new PlaybackService(backend, clock: clock);
        var credentials = new FixtureCredentials();
        var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(2)), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials, settings: db.Settings);
        var window = new MainWindow(vm, logos) { Width = 1360, Height = 900, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            await vm.InitializeSettingsAsync(); window.Show(); vm.CurrentTabIndex = 4; await SettleAsync(window);
            var continuity = (CheckBox)window.FindName("ContinuityCheckBox"); var quality = (ComboBox)window.FindName("LiveQualityBox");
            Assert.True(continuity.IsChecked); Assert.Equal(LiveQualityPreference.Automatic, quality.SelectedValue);
            quality.SelectedValue = LiveQualityPreference.DataSaver; await vm.ApplyLivePreferencesAsync();
            Assert.Equal(LiveQualityPreference.DataSaver, (await db.Settings.GetAppSettingsAsync()).LiveQuality);
            continuity.IsChecked = false; await vm.ApplyLivePreferencesAsync();
            Assert.False((await db.Settings.GetAppSettingsAsync()).PrioritizeContinuity);
            continuity.IsChecked = true; await vm.ApplyLivePreferencesAsync();
            await playback.PlayAsync(new("https://provider.invalid/fixture.m3u8"));
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await playback.ReadDetailsAsync();
            foreach (var (frames, bytes) in new[] { (10, 1_000_000L), (35, 2_000_000L) })
            {
                backend.Progress = new(2000, frames, 0, ReceivedBytes: bytes); var reads = backend.ProgressReads;
                clock.Advance(TimeSpan.FromSeconds(1)); await WaitAsync(() => backend.ProgressReads > reads); await playback.ReadDetailsAsync();
            }
            await vm.RefreshPlaybackDetailsAsync(); await SettleAsync(window);
            var settingsLabel = (TextBlock)window.FindName("SettingsPlaybackDiagnostics");
            Assert.Equal(vm.PlaybackDiagnosticsText, settingsLabel.Text);
            vm.CurrentTabIndex = 1; await SettleAsync(window);
            var label = (TextBlock)window.FindName("PlaybackDiagnosticsLabel");
            Assert.Equal(vm.PlaybackDiagnosticsText, label.Text); Assert.Contains("25", label.Text, StringComparison.Ordinal);
            Assert.Contains("20 s", label.Text, StringComparison.Ordinal); Assert.Equal(1, backend.Peak);
            vm.CurrentTabIndex = 4; await SettleAsync(window); settingsLabel.BringIntoView(); await SettleAsync(window);
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render((Visual)window.Content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(artifacts, "playback-continuity-settings.png")); encoder.Save(file);
        }
        finally { window.Close(); await vm.DisposeAsync(); }
    }
}
