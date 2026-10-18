using StreamDesk.Probe.Core;

namespace StreamDesk.Application;

public interface IContentProvider
{
    Task<MediaCatalog> GetMediaCatalogAsync(ContentKind kind, CancellationToken cancellationToken = default);
    Task<MediaItem> GetMovieInfoAsync(MediaItem movie, CancellationToken cancellationToken = default);
    Task<MediaCatalog> GetEpisodesAsync(string seriesId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EpgProgram>> GetEpgAsync(string channelId, CancellationToken cancellationToken = default);
    Uri CreateMediaUri(MediaItem item);
}
public interface IContentStore
{
    Task<MediaCatalog?> LoadCatalogAsync(string profileId, ContentKind kind, string scope = "", CancellationToken cancellationToken = default);
    Task SaveCatalogAsync(string profileId, ContentKind kind, string scope, MediaCatalog catalog, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaProgress>> LoadProgressAsync(string profileId, CancellationToken cancellationToken = default);
    Task SaveProgressAsync(string profileId, MediaProgress progress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EpgProgram>?> LoadEpgAsync(string profileId, string channelId, CancellationToken cancellationToken = default);
    Task SaveEpgAsync(string profileId, string channelId, IReadOnlyList<EpgProgram> programs, CancellationToken cancellationToken = default);
}
public interface IEpgImporter
{
    Task<IReadOnlyList<EpgProgram>> ReadAsync(Stream input, string epgId, CancellationToken cancellationToken = default);
}
