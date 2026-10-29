using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class PlaybackContinuityTests
{
    [Fact]
    public async Task ExistingSettingsGainSafeDefaultsAndPreserveTheSelectedBuffer()
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync();
        await db.Settings.SaveAppSettingsAsync(new(BufferMilliseconds: 8000, SoftwareDecoding: true));
        await using (var connection = new SqliteConnection($"Data Source={db.PathName};Pooling=False"))
        {
            await connection.OpenAsync(); using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE AppSettings DROP COLUMN PrioritizeContinuity; ALTER TABLE AppSettings DROP COLUMN LiveQuality;";
            await command.ExecuteNonQueryAsync();
        }
        await db.Settings.InitializeAsync(); await db.Settings.InitializeAsync();
        var migrated = await db.Settings.GetAppSettingsAsync();
        Assert.True(migrated.PrioritizeContinuity); Assert.Equal(8000, migrated.BufferMilliseconds); Assert.True(migrated.SoftwareDecoding);
        await db.Settings.SaveAppSettingsAsync(migrated with { PrioritizeContinuity = false, LiveQuality = LiveQualityPreference.DataSaver });
        var reloaded = await db.Settings.GetAppSettingsAsync();
        Assert.False(reloaded.PrioritizeContinuity); Assert.Equal(LiveQualityPreference.DataSaver, reloaded.LiveQuality);
        await using var bad = new SqliteConnection($"Data Source={db.PathName};Pooling=False"); await bad.OpenAsync();
        using var invalid = bad.CreateCommand(); invalid.CommandText = "UPDATE AppSettings SET LiveQuality=999 WHERE Id=1"; await invalid.ExecuteNonQueryAsync();
        Assert.Equal(LiveQualityPreference.Automatic, (await db.Settings.GetAppSettingsAsync()).LiveQuality);
    }

    [Fact]
    public async Task HttpLiveGetsReserveAndLongerHlsTimeoutButVodAndLocalFilesKeepTheirBuffer()
    {
        var backend = new ControlledBackend(); var clock = new ManualPlaybackClock();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(15), TimeSpan.Zero), clock);
        service.Options = new(3500, PrioritizeContinuity: true);
        await service.PlayAsync(new("https://provider.invalid/live.m3u8"));
        Assert.Equal(5000, backend.Opens.Last().Options.BufferMilliseconds);
        Assert.Equal(20000, service.Diagnostics.AdaptiveLiveDelayMilliseconds);
        clock.Advance(TimeSpan.FromSeconds(16)); await service.ReadDetailsAsync();
        Assert.Equal(PlaybackState.Loading, service.Snapshot.State); Assert.Single(backend.Opens);
        clock.Advance(TimeSpan.FromSeconds(10));
        await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
        Assert.Equal(6500, backend.Opens.Last().Options.BufferMilliseconds);
        service.Options = service.Options with { OnDemand = true };
        await service.PlayAsync(new("https://provider.invalid/movie.mp4"));
        Assert.Equal(3500, backend.Opens.Last().Options.BufferMilliseconds);
        Assert.Equal(0, service.Diagnostics.AdaptiveLiveDelayMilliseconds);
        service.Options = service.Options with { OnDemand = false };
        await service.PlayAsync(new Uri(Path.Combine(Path.GetTempPath(), "invented-local.avi")));
        Assert.Equal(3500, backend.Opens.Last().Options.BufferMilliseconds); Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task DiagnosticsMeasureSignalDeltasAndExcludePauseSeekAndOldSessions()
    {
        var backend = new ControlledBackend { Details = new(1000, 60000, true, [], []) }; var clock = new ManualPlaybackClock();
        await using var service = new PlaybackService(backend, clock: clock);
        await service.PlayAsync(new("https://provider.invalid/live.ts"));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing)); await service.ReadDetailsAsync();
        async Task Sample(long frames, long? bytes, int seconds = 1)
        {
            backend.Progress = new(2000, frames, 0, ReceivedBytes: bytes, LostVideoFrames: 3);
            var reads = backend.ProgressReads; clock.Advance(TimeSpan.FromSeconds(seconds));
            await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads); await service.ReadDetailsAsync();
        }
        await Sample(10, 1_000_000); Assert.Null(service.Diagnostics.ReceivedMegabitsPerSecond);
        await Sample(35, 2_000_000); Assert.Equal(8d, service.Diagnostics.ReceivedMegabitsPerSecond); Assert.Equal(25d, service.Diagnostics.VideoFramesPerSecond);
        await Sample(35, 3_000_000, 4); Assert.Equal(1, service.Diagnostics.RebufferCount);
        await Sample(35, 4_000_000); Assert.Equal(PlaybackObservation.DataWithoutVideoProgress, service.Diagnostics.Observation);
        await Sample(60, 5_000_000); Assert.Equal(PlaybackObservation.Progressing, service.Diagnostics.Observation);
        Assert.Equal(2d, service.Diagnostics.WaitingSeconds);
        await service.PauseAsync(); clock.Advance(TimeSpan.FromMinutes(1)); await service.ReadDetailsAsync();
        Assert.Equal(PlaybackObservation.Paused, service.Diagnostics.Observation); Assert.Null(service.Diagnostics.ReceivedMegabitsPerSecond);
        await service.ResumeAsync(); Assert.True(await service.SeekAsync(10000));
        await Sample(60, 5_000_000); Assert.Null(service.Diagnostics.VideoFramesPerSecond);
        await Sample(85, 100); Assert.Null(service.Diagnostics.ReceivedMegabitsPerSecond); // Native counters reset/wrap.
        Assert.Equal(1, service.Diagnostics.RebufferCount);
        await service.PlayAsync(new("https://provider.invalid/other.ts"));
        Assert.Equal(0, service.Diagnostics.RebufferCount); Assert.Null(service.Diagnostics.VideoFramesPerSecond);
        await service.StopAsync(); Assert.Equal(PlaybackDiagnostics.Empty, service.Diagnostics);
    }

    [Fact]
    public async Task PreferencesPreferDeclaredHlsAndApplyThroughTheSingleStreamPipeline()
    {
        using var db = new TestDatabase(); await db.Settings.InitializeAsync(); await db.AddProfileAsync("first");
        var backend = new ControlledBackend(); var service = new PlaybackService(backend); var credentials = new FixtureCredentials();
        await using var vm = new MainViewModel((_, _, _) => new BothFormatsProvider(), service,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials, settings: db.Settings);
        await vm.InitializeSettingsAsync(); await vm.LoadProfilesAsync(); await vm.ConnectProfileAsync(); await vm.LoadCatalogAsync();
        Assert.True(vm.PrioritizeContinuity); Assert.Equal("m3u8", vm.SelectedFormat);
        vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync();
        vm.LiveQuality = LiveQualityPreference.DataSaver; await vm.ApplyLivePreferencesAsync();
        Assert.Equal(LiveQualityPreference.DataSaver, backend.Opens.Last().Options.LiveQuality);
        Assert.Equal(5000, backend.Opens.Last().Options.BufferMilliseconds); Assert.Equal(1, backend.Peak);
        Assert.Equal(LiveQualityPreference.DataSaver, (await db.Settings.GetAppSettingsAsync()).LiveQuality);
        await vm.RefreshPlaybackDetailsAsync(); Assert.Contains("20 s", vm.PlaybackDiagnosticsText, StringComparison.Ordinal);
    }

    private sealed class BothFormatsProvider : IIptvProvider
    {
        private readonly CatalogSnapshot _catalog = CatalogFixtures.Create(2);
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts", "m3u8"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(_catalog.Categories);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_catalog.Channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new($"https://provider.invalid/live/{channel.Id}.{format}");
        public void Dispose() { }
    }
}
