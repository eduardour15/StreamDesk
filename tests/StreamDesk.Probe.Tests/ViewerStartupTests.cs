using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class ViewerStartupTests
{
    [Fact]
    public async Task SelectingAnotherProfileDuringStartupNeverAutoplaysThatProfile()
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first"); await db.AddProfileAsync("second");
        var store = new SqliteViewerStore(db.PathName, false);
        foreach (var id in new[] { "first", "second" })
        {
            await db.Store.ReplaceAsync(id, CatalogFixtures.Create(10));
            await store.RecordPlayedAsync(id, "live", "000003", DateTimeOffset.UtcNow);
            await store.SetAutoPlayAsync(id, true);
        }
        await store.SetLastProfileAsync("first");
        var delayed = new DeferredViewerStore(store); var credentials = new FixtureCredentials(); var backend = new ControlledBackend();
        await using var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(10)), new PlaybackService(backend),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials, delayed);
        var startup = vm.LoadProfilesAsync(); await delayed.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            vm.ActiveProfile = vm.Profiles.Single(item => item.Id == "second");
            await PlaybackTests.UntilAsync(() => !vm.IsWorking && vm.AutoPlayLast && vm.HasLastChannel);
        }
        finally { delayed.Release.TrySetResult(); }
        await startup;
        Assert.Empty(backend.Opens);
    }

    private sealed class DeferredViewerStore(IViewerStore inner) : IViewerStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ViewerLibrary> LoadAsync(string id, CancellationToken token = default)
        {
            if (id == "first") { Entered.TrySetResult(); await Release.Task; }
            return await inner.LoadAsync(id); // Simulate completion after cancellation.
        }
        public Task SetFavoriteAsync(string p, string t, string c, bool f, CancellationToken token = default) => inner.SetFavoriteAsync(p, t, c, f, token);
        public Task RecordPlayedAsync(string p, string t, string c, DateTimeOffset d, CancellationToken token = default) => inner.RecordPlayedAsync(p, t, c, d, token);
        public Task SetAutoPlayAsync(string p, bool e, CancellationToken token = default) => inner.SetAutoPlayAsync(p, e, token);
        public Task<string?> GetLastProfileAsync(CancellationToken token = default) => inner.GetLastProfileAsync(token);
        public Task SetLastProfileAsync(string p, CancellationToken token = default) => inner.SetLastProfileAsync(p, token);
    }
}
