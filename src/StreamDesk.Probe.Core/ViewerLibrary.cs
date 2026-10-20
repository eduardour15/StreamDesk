namespace StreamDesk.Probe.Core;

public sealed record RecentChannel(string ChannelId, DateTimeOffset PlayedAt);
public sealed record ViewerLibrary(IReadOnlySet<string> Favorites, IReadOnlyList<RecentChannel> Recents, bool AutoPlayLast = false, int AmplificationDb = 0);
