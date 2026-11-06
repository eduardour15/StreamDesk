using System.Reflection;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class MediaCatalogGridTests
{
    [Fact]
    public async Task PosterGrid_PaginatesFilteredResults_AndCompactListShowsAllVirtualizableItems()
    {
        using var db = new TestDatabase();
        await db.AddProfileAsync("profile-1");
        var credentials = new FixtureCredentials();
        var viewer = new SqliteViewerStore(db.PathName, pooling: false);
        var vm = new MainViewModel(
            (_, _, _) => new FixtureProvider(CatalogFixtures.Create(10)),
            new FixturePlayback(),
            new ProfileService(db.Settings, credentials),
            new FixtureLogger(),
            new CatalogService(db.Store),
            credentials,
            viewer);
        await vm.LoadProfilesAsync();
        await vm.ConnectProfileAsync();
        await vm.LoadCatalogAsync();

        var items = Enumerable.Range(0, 100)
            .Select(index => new MediaItem($"movie-{index:D3}", $"Title {index:D3}", ContentKind.Movie, Extension: "mp4"))
            .ToArray();
        typeof(MainViewModel).GetField("_movies", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(vm, new MediaCatalog([], items, DateTimeOffset.UtcNow));

        vm.CurrentTabIndex = 5;
        var filter = typeof(MainViewModel).GetMethod("FilterMediaAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)filter.Invoke(vm, [false])!;

        Assert.True(vm.IsMediaGridView);
        Assert.Equal(100, vm.MediaItems.Count);
        Assert.Equal(48, vm.VisibleMediaItems.Count);
        Assert.True(vm.CanNextMediaPage);

        vm.NextMediaPage();
        Assert.Equal("movie-048", vm.VisibleMediaItems[0].Id);
        vm.NextMediaPage();
        Assert.Equal(4, vm.VisibleMediaItems.Count);
        Assert.False(vm.CanNextMediaPage);

        vm.SelectedSortOrder = MainViewModel.MediaSortOrder.TitleDesc;
        await (Task)filter.Invoke(vm, [false])!;
        Assert.Equal("movie-099", vm.VisibleMediaItems[0].Id);
        Assert.Equal(3, vm.MediaPageCount);
        Assert.Contains("página 1 de 3", vm.MediaPageSummary);

        await vm.ToggleMediaFavoriteAsync(items[10]);
        Assert.Contains("movie-010", vm.MediaMovieFavoriteIds);
        Assert.Contains("movie-010", (await viewer.LoadAsync(vm.ActiveProfile!.Id)).MovieFavorites);
        vm.MediaFavoritesOnly = true;
        await (Task)filter.Invoke(vm, [false])!;
        Assert.Equal("movie-010", Assert.Single(vm.MediaItems).Id);
        vm.MediaFavoritesOnly = false;
        await (Task)filter.Invoke(vm, [false])!;

        var seriesItem = new MediaItem("series-1", "Series", ContentKind.Series);
        typeof(MainViewModel).GetField("_series", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(vm, new MediaCatalog([], [seriesItem], DateTimeOffset.UtcNow));
        await vm.ToggleMediaFavoriteAsync(seriesItem);
        Assert.Contains("series-1", (await viewer.LoadAsync(vm.ActiveProfile!.Id)).SeriesFavorites);
        Assert.Equal("movie-010", Assert.Single(vm.FavoriteMovies).Id);
        Assert.Equal("series-1", Assert.Single(vm.FavoriteSeries).Id);

        vm.IsMediaListView = true;
        Assert.True(vm.IsMediaListView);
        Assert.False(vm.IsMediaGridView);
        Assert.Equal(100, vm.MediaItems.Count);
        vm.CurrentTabIndex = 2;
        Assert.True(vm.ShowFavorites);
        Assert.False(vm.ShowLiveCatalog);
        Assert.Equal(2, vm.FavoriteCount);
        vm.SelectedFavoriteTab = 2;
        Assert.Equal("movie-010", Assert.Single(vm.FavoriteMediaItems).Id);
        vm.SelectedFavoriteTab = 3;
        Assert.Equal("series-1", Assert.Single(vm.FavoriteMediaItems).Id);
        await vm.DisposeAsync();
    }
}
