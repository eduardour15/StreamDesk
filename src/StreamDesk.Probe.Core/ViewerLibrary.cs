namespace StreamDesk.Probe.Core;

public sealed record RecentChannel(string ChannelId, DateTimeOffset PlayedAt);
public sealed record ViewerLibrary(IReadOnlySet<string> Favorites, IReadOnlyList<RecentChannel> Recents, bool AutoPlayLast = false, int AmplificationDb = 0)
{
    public IReadOnlySet<string> MovieFavorites { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> SeriesFavorites { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}
