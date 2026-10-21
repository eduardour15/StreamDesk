using System.Diagnostics;
using System.Text.Json;
using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Tests;

[Collection("NativePlayback")]
public sealed class SearchPerformanceTests
{
    [Fact]
    public void WarmIndexMatchesPreviousPredicateAndRecordsFilterCostWithoutUiOrDebounce()
    {
        var rows = new List<object>();
        foreach (var count in new[] { 5000, 20000 })
        {
            var catalog = new MediaCatalog([], Enumerable.Range(0, count).Select(i =>
                new MediaItem(i.ToString(), $"Película Águila {i:D5}", ContentKind.Movie, "fiction")).ToArray(), DateTimeOffset.UtcNow);
            var build = Stopwatch.StartNew(); var index = new MediaCatalogSearch(catalog); build.Stop();
            IReadOnlyList<MediaItem> Previous(string query)
            {
                var key = CatalogSearch.Normalize(query);
                return catalog.Items.Where(item => CatalogSearch.Normalize(item.Name).Contains(key, StringComparison.Ordinal)).ToArray();
            }
            IReadOnlyList<MediaItem> Indexed(string query) => index.Filter(query, null, null);
            var queries = new[] { "pelicula aguila", "inexistente", "" };
            foreach (var query in queries) Assert.Equal(Previous(query).Select(i => i.Id), Indexed(query).Select(i => i.Id));
            object Measure(Func<string, IReadOnlyList<MediaItem>> filter)
            {
                var samples = new List<double>(); var allocations = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 12; i++)
                {
                    var start = Stopwatch.GetTimestamp(); filter(queries[i % queries.Length]);
                    samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
                return new { P50Milliseconds = samples.Order().ElementAt(5), P95Milliseconds = samples.Max(), AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocations };
            }
            rows.Add(new { Count = count, Samples = 12, IndexBuildMilliseconds = build.Elapsed.TotalMilliseconds, PreviousPredicate = Measure(Previous), Indexed = Measure(Indexed) });
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "StreamDesk.sln"))) root = root.Parent ?? throw new InvalidOperationException();
        var directory = Path.Combine(root.FullName, "artifacts"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "flux-filter-cost.json"), JsonSerializer.Serialize(new
        {
            Source = "Same-process reference of the previous predicate versus warm index; excludes UI, network and 180ms debounce; not a second pre-change app run",
            Measurements = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
