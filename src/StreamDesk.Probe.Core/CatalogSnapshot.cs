namespace StreamDesk.Probe.Core;

public sealed record CatalogSnapshot(
    IReadOnlyList<XtreamCategory> Categories,
    IReadOnlyList<XtreamChannel> Channels,
    DateTimeOffset SyncedAt,
    IReadOnlySet<string>? FavoriteChannelIds = null);
