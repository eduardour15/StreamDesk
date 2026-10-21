using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class FluidityTests
{
    [Fact]
    public async Task ChannelChangeDuringNativeProgressReadCannotPublishPreviousStall()
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, clock: clock);
        await service.PlayAsync(new("https://provider.invalid/first.ts"));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
        foreach (var frames in new[] { 10, 20 })
        {
            backend.Progress = new(1000, frames, 0); var reads = backend.ProgressReads;
            clock.Advance(TimeSpan.FromSeconds(1));
            await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads);
            await service.ReadDetailsAsync();
        }
        var pendingRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.ProgressGate = pendingRead; var previousReads = backend.ProgressReads;
        clock.Advance(TimeSpan.FromSeconds(5));
        await PlaybackTests.UntilAsync(() => backend.ProgressReads > previousReads);
        var states = new System.Collections.Concurrent.ConcurrentQueue<PlaybackState>();
        service.StateChanged += (_, state) => states.Enqueue(state.State);
        var switching = service.PlayAsync(new("https://provider.invalid/second.ts"));
        backend.ProgressGate = null; pendingRead.SetResult(); await switching.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(PlaybackState.Buffering, states);
        Assert.Equal(PlaybackState.Loading, service.Snapshot.State); Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task RapidAudioChangesAreCoalescedWithoutLosingLastValues()
    {
        var backend = new ControlledBackend(); await using var service = new PlaybackService(backend);
        await service.InitializeAsync();
        var writes = backend.AudioWrites;
        backend.AudioGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Volume = 1;
        await PlaybackTests.UntilAsync(() => backend.AudioWrites == writes + 1);
        for (var value = 2; value <= 100; value++) service.Volume = value;
        service.IsMuted = true; service.AmplificationDb = 6;
        backend.AudioGate.SetResult();
        await PlaybackTests.UntilAsync(() => backend.Audio == (100, true) && backend.AmplificationDb == 6);
        Assert.InRange(backend.AudioWrites - writes, 1, 3);
    }

    [Fact]
    public async Task SilentStallShowsBufferingRecoversAndIgnoresPauseSeekAndOldSession()
    {
        var clock = new ManualPlaybackClock();
        var backend = new ControlledBackend { Details = new(2000, 60000, true, [], []) };
        await using var service = new PlaybackService(backend, clock: clock);
        service.Options = new(OnDemand: true);
        await service.PlayAsync(new("https://provider.invalid/movie.mp4"));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
        async Task Sample(PlaybackProgress progress, int seconds = 1)
        {
            backend.Progress = progress;
            var reads = backend.ProgressReads;
            clock.Advance(TimeSpan.FromSeconds(seconds));
            await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads);
            await service.ReadDetailsAsync();
        }
        await Sample(new(1000, 20, 0));
        await Sample(new(2000, 40, 0));
        await Sample(new(2000, 40, 0), 3);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        await Sample(new(2000, 40, 0));
        Assert.Equal(PlaybackState.Buffering, service.Snapshot.State);
        Assert.Single(backend.Opens);
        await Sample(new(3000, 60, 0));
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        await service.PauseAsync();
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Buffering));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        clock.Advance(TimeSpan.FromMinutes(2));
        await service.ReadDetailsAsync();
        Assert.Equal(PlaybackState.Paused, service.Snapshot.State);
        await service.ResumeAsync();
        await Sample(new(3000, 60, 0));
        await Sample(new(4000, 80, 0));
        Assert.True(await service.SeekAsync(30000));
        await Sample(new(30000, 80, 0), 20);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        await service.StopAsync();
        var readCount = backend.ProgressReads;
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(PlaybackState.Stopped, service.Snapshot.State);
        Assert.Equal(readCount, backend.ProgressReads);
        Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task UnknownProgressDoesNotStallAndAudioOnlyUsesItsOwnProgress()
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, clock: clock);
        await service.PlayAsync(new("https://provider.invalid/radio.ts"));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
        async Task Tick(PlaybackProgress? value, int seconds)
        {
            backend.Progress = value; var reads = backend.ProgressReads;
            clock.Advance(TimeSpan.FromSeconds(seconds));
            await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads);
            await service.ReadDetailsAsync();
        }
        await Tick(null, 30); await Tick(new(0, 0, 0), 30);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        await Tick(new(0, 0, 10), 1); await Tick(new(0, 0, 20), 1); await Tick(new(0, 0, 30), 20);
        Assert.Equal(PlaybackState.Playing, service.Snapshot.State);
        Assert.Single(backend.Opens);
    }

    [Fact]
    public async Task SilentStallTimeoutConsumesOnlyExistingRetryBudget()
    {
        var clock = new ManualPlaybackClock(); var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(15), TimeSpan.Zero), clock);
        await service.PlayAsync(new("https://provider.invalid/live.ts"));
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
            await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
            foreach (var (frames, seconds) in new[] { (10, 1), (20, 1), (20, 5) })
            {
                backend.Progress = new(2000, frames, 0); var reads = backend.ProgressReads;
                clock.Advance(TimeSpan.FromSeconds(seconds));
                await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads);
                await service.ReadDetailsAsync();
            }
            await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Buffering);
            clock.Advance(TimeSpan.FromSeconds(16));
            if (attempt < 3) await PlaybackTests.UntilAsync(() => backend.Opens.Count == attempt + 1);
            else await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Failed && !service.Snapshot.WillRetry);
        }
        Assert.Equal(3, backend.Opens.Count); Assert.Equal(2, service.Snapshot.RetryCount); Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public void MediaIndexPreservesNormalizationCategoryRestrictionsAndCancellation()
    {
        var catalog = new MediaCatalog([new("music", "Música")],
            [new("1", "Película Águila", ContentKind.Movie, "music"), new("2", "Película Águila", ContentKind.Movie, "adult")], DateTimeOffset.UtcNow);
        var search = new MediaCatalogSearch(catalog);
        Assert.Equal("1", Assert.Single(search.Filter("PELICULA aguila", "music", null)).Id);
        Assert.Equal("1", Assert.Single(search.Filter("", null, new HashSet<string> { "adult" })).Id);
        Assert.Empty(search.Filter("inexistente", null, null));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => search.Filter("", null, null, cancel.Token));
    }

    [Fact]
    public async Task MediaSearchLatestQueryWinsAndTrackRefreshPreservesPendingSelection()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend { Details = new(1000, 60000, true, [new(1, "Español"), new(2, "Inglés")], [new(-1, "Desactivados")], 1) };
        var clock = new ManualPlaybackClock();
        var playback = new PlaybackService(backend, clock: clock);
        await using var vm = new MainViewModel((_, _, _) => new LargeMediaProvider(20000), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        await vm.LoadProfilesAsync(); await vm.ProbeContentAsync(); vm.CurrentTabIndex = 5;
        await PlaybackTests.UntilAsync(() => !vm.IsMediaFiltering && vm.MediaItems.Count == 20000);
        var searchesApplied = 0;
        void SearchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        { if (args.PropertyName == nameof(vm.MediaItems)) searchesApplied++; }
        vm.PropertyChanged += SearchChanged;
        vm.MediaSearch = "título"; await Task.Delay(40);
        vm.MediaSearch = "inexistente"; await Task.Delay(40);
        vm.MediaSearch = "pelicula aguila 00002";
        await PlaybackTests.UntilAsync(() => !vm.IsMediaFiltering && vm.MediaItems.Count == 1);
        vm.PropertyChanged -= SearchChanged; Assert.Equal(1, searchesApplied);
        Assert.Equal("2", vm.MediaItems[0].Id);
        vm.SelectedMedia = vm.MediaItems[0]; await vm.PlayMediaAsync();
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => vm.IsPlaying);
        await vm.RefreshPlaybackDetailsAsync();
        var tracks = vm.AudioTracks; vm.SelectedAudioTrack = vm.AudioTracks[1];
        var notifications = new List<string?>(); vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        backend.Details = backend.Details with { TimeMilliseconds = 2000, AudioTracks = [new(1, "Español"), new(2, "Inglés")] };
        await vm.RefreshPlaybackDetailsAsync();
        Assert.Same(tracks, vm.AudioTracks); Assert.Equal(2, vm.SelectedAudioTrack!.Id);
        Assert.DoesNotContain(nameof(vm.AudioTracks), notifications); Assert.DoesNotContain(nameof(vm.SelectedAudioTrack), notifications);
        Assert.Contains(nameof(vm.PositionSeconds), notifications);
        vm.MediaSearch = "título"; vm.CurrentTabIndex = 6; vm.MediaSearch = "pelicula aguila 00004";
        await PlaybackTests.UntilAsync(() => !vm.IsMediaFiltering && vm.MediaItems.Count == 1);
        Assert.Equal(ContentKind.Series, vm.MediaItems[0].Kind);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));
        await PlaybackTests.UntilAsync(() => vm.IsVideoLoading && playback.Snapshot.WillRetry);
        Assert.True(vm.CanStopPlayback); Assert.Contains("recuperar", vm.VideoLoadingMessage, StringComparison.Ordinal);
        var opens = backend.Opens.Count; await vm.StopPlaybackAsync();
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(vm.IsVideoLoading); Assert.Equal(opens, backend.Opens.Count);
    }

    [Fact]
    public async Task LiveStreamAdaptiveBufferResetsRetriesAfterStablePlaybackAndSurvivesBufferingFlicker()
    {
        var clock = new ManualPlaybackClock();
        var backend = new ControlledBackend();
        await using var service = new PlaybackService(backend, new(TimeSpan.FromSeconds(15), TimeSpan.Zero), clock);
        service.Options = new(3500);
        await service.PlayAsync(new("https://provider.invalid/live.ts"));
        Assert.Equal(3500, backend.Opens.Last().Options.BufferMilliseconds);

        // Emit Playing, then a transient Network failure -> 1st retry scales buffer to 3500 + 1500 = 5000 ms
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Failed, PlaybackFailure.Network));
        await PlaybackTests.UntilAsync(() => backend.Opens.Count == 2);
        Assert.Equal(5000, backend.Opens.Last().Options.BufferMilliseconds);

        // Now play stably for > 60 seconds -> retry counter resets to 0
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing));
        await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);
        async Task TickProgress(int frames, int seconds)
        {
            backend.Progress = new(seconds * 1000, frames, 0);
            var reads = backend.ProgressReads;
            clock.Advance(TimeSpan.FromSeconds(seconds));
            await PlaybackTests.UntilAsync(() => backend.ProgressReads > reads);
            await service.ReadDetailsAsync();
        }
        await TickProgress(10, 1);
        await TickProgress(20, 1);
        await TickProgress(600, 61);
        Assert.Equal(0, service.Snapshot.RetryCount);

        // Simulate native Buffering -> Playing flicker while frames remain frozen at 600
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Buffering, BufferPercent: 40));
        backend.Emit(new(backend.Opens.Last().Id, PlaybackSignalKind.Playing, BufferPercent: 100));
        await PlaybackTests.UntilAsync(() => service.Snapshot.State == PlaybackState.Playing);

        // Frozen frames for > 7 seconds (2x 3500ms base buffer) still trigger SilentStall despite the Buffering flicker
        await TickProgress(600, 8);
        Assert.Equal(PlaybackState.Buffering, service.Snapshot.State);
        Assert.Equal(1, backend.Peak);
    }

    [Fact]
    public async Task LiveFormatFallbackAndPersistedStabilitySettingsWorkWithoutExtraActiveStreams()
    {
        using var db = new TestDatabase();
        await db.AddProfileAsync("first");
        var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend, new(TimeSpan.FromSeconds(5), TimeSpan.Zero));
        var fixture = CatalogFixtures.Create(5);
        await using (var vm = new MainViewModel((_, _, _) => new DualFormatProvider(fixture), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials,
            settings: db.Settings))
        {
            await vm.InitializeSettingsAsync();
            Assert.Equal(3500, vm.BufferMilliseconds);

            await vm.CycleStabilityPresetAsync();
            Assert.Equal(5000, vm.BufferMilliseconds);
            var saved = await db.Settings.GetAppSettingsAsync();
            Assert.Equal(5000, saved.BufferMilliseconds);

            await vm.LoadProfilesAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsCatalogLoading && !vm.IsBusy);
            await vm.LoadCatalogAsync();
            await PlaybackTests.UntilAsync(() => !vm.IsCatalogLoading && vm.Channels.Count == 5);
            vm.SelectedChannel = vm.Channels[0];
            vm.SelectedFormat = "ts";
            await vm.PlaySelectedAsync();
            Assert.EndsWith(".ts", backend.Opens.Last().Source.AbsolutePath, StringComparison.Ordinal);

            // Exhaust retries on .ts -> automatic fallback to .m3u8
            for (var i = 0; i < 3; i++)
            {
                var currentId = backend.Opens.Last().Id;
                backend.Emit(new(currentId, PlaybackSignalKind.Failed, PlaybackFailure.Network));
                await PlaybackTests.UntilAsync(() => backend.Opens.Count >= i + 2);
            }
            Assert.Equal("m3u8", vm.SelectedFormat);
            Assert.EndsWith(".m3u8", backend.Opens.Last().Source.AbsolutePath, StringComparison.Ordinal);
            Assert.Equal(1, backend.Peak);
        }
    }

    private sealed class DualFormatProvider(CatalogSnapshot snapshot) : IIptvProvider
    {
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new XtreamAccount("Active", 0, 1, false, null, ["ts", "m3u8"]));
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot.Categories);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot.Channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new($"https://provider.invalid/live/{channel.Id}.{format}");
        public void Dispose() { }
    }
}

internal sealed class ManualPlaybackClock : TimeProvider
{
    private long _ticks;
    private readonly List<ManualTimer> _timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_timers) _timers.Add(timer);
        timer.Change(dueTime, period); return timer;
    }
    public void Advance(TimeSpan elapsed)
    {
        Interlocked.Add(ref _ticks, elapsed.Ticks);
        ManualTimer[] timers; lock (_timers) timers = _timers.ToArray();
        foreach (var timer in timers) timer.Fire();
    }
    private sealed class ManualTimer(ManualPlaybackClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long _due = long.MaxValue, _period;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        { lock (clock._timers) { _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.GetTimestamp() + dueTime.Ticks; _period = period.Ticks; return true; } }
        public void Fire()
        {
            lock (clock._timers)
            {
                if (_due > clock.GetTimestamp()) return;
                _due = _period > 0 ? clock.GetTimestamp() + _period : long.MaxValue;
            }
            callback(state);
        }
        public void Dispose() { lock (clock._timers) { _due = long.MaxValue; clock._timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
