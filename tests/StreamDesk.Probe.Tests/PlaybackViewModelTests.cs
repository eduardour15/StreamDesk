using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class PlaybackViewModelTests
{
    [Fact]
    public async Task NavigationUsesVisibleOrderAndSuspendDoesNotAutoplay()
    {
        using var db = new TestDatabase();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        var credentials = new FixtureCredentials();
        await using var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(20)), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        await vm.InitializePlaybackAsync();
        await vm.ConnectAsync("https://provider.invalid", "fixture-user", "fixture-password");
        await vm.LoadCatalogAsync();
        vm.SearchText = "repetido";
        await PlaybackTests.UntilAsync(() => vm.Channels.Count == 2);
        vm.SelectedChannel = vm.Channels[0];
        await vm.PlaySelectedAsync();
        await vm.NavigateChannelAsync(1); Assert.Equal(vm.Channels[1].Id, vm.SelectedChannel?.Id);
        var count = backend.Opens.Count;
        await vm.NavigateChannelAsync(1); Assert.Equal(count, backend.Opens.Count);
        await vm.NavigateChannelAsync(-1); Assert.Equal(vm.Channels[0].Id, vm.SelectedChannel?.Id);
        vm.Volume = 30; vm.IsMuted = true; vm.BufferMilliseconds = 2000; vm.SoftwareDecoding = true;
        await vm.PlaySelectedAsync();
        Assert.Equal((30, true), backend.Audio);
        Assert.Equal(new PlaybackOptions(2000, HardwareDecoding.Software), backend.Opens.Last().Options);
        var beforeSuspend = backend.Opens.Count;
        await vm.SuspendPlaybackAsync(); await Task.Delay(60);
        Assert.Equal(beforeSuspend, backend.Opens.Count); Assert.Equal(0, backend.Active);
        Assert.Equal(PlaybackState.Stopped, playback.Snapshot.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiredOrOccupiedAccountNeverOpensAnotherStream(bool expired)
    {
        using var db = new TestDatabase(); var credentials = new FixtureCredentials();
        var backend = new ControlledBackend();
        var account = new XtreamAccount("Active", expired ? 0 : 1, 1, false, expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : null, ["ts"]);
        await using var vm = new MainViewModel((_, _, _) => new AccountProvider(account), new PlaybackService(backend),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        await vm.ConnectAsync("https://provider.invalid", "fixture-user", "fixture-password"); await vm.LoadCatalogAsync();
        vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync();
        Assert.Empty(backend.Opens);
        Assert.Contains(expired ? "venció" : "límite", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileChangeDuringBufferingCancelsPendingStreamAndOldEvents()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        var backend = new ControlledBackend(); var playback = new PlaybackService(backend); var credentials = new FixtureCredentials();
        await using var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(10)), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);
        await vm.LoadProfilesAsync(); await PlaybackTests.UntilAsync(() => !vm.IsWorking && vm.Channels.Count == 10);
        await vm.ConnectProfileAsync(); vm.SelectedChannel = vm.Channels[0]; await vm.PlaySelectedAsync();
        var attempt = backend.Opens.Last().Id;
        backend.Emit(new(attempt, PlaybackSignalKind.Buffering, BufferPercent: 15));
        await PlaybackTests.UntilAsync(() => playback.Snapshot.State == PlaybackState.Buffering);
        vm.ActiveProfile = vm.Profiles.Single(profile => profile.Id == "second");
        await PlaybackTests.UntilAsync(() => !vm.IsWorking && playback.Snapshot.State == PlaybackState.Stopped);
        backend.Emit(new(attempt, PlaybackSignalKind.Playing)); await Task.Delay(40);
        Assert.Equal(0, backend.Active); Assert.Equal(PlaybackState.Stopped, playback.Snapshot.State);
    }

    private sealed class AccountProvider(XtreamAccount account) : IIptvProvider
    {
        private readonly CatalogSnapshot _catalog = CatalogFixtures.Create(2);
        public bool UsesUnencryptedTransport => false;
        public Task<XtreamAccount> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(account);
        public Task<IReadOnlyList<XtreamCategory>> GetLiveCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(_catalog.Categories);
        public Task<IReadOnlyList<XtreamChannel>> GetLiveChannelsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_catalog.Channels);
        public Uri CreateLiveStreamUri(XtreamChannel channel, string format) => new("https://provider.invalid/fixture.ts");
        public void Dispose() { }
    }
}
