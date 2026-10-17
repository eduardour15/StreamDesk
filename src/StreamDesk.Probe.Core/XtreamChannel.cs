namespace StreamDesk.Probe.Core;

public sealed record XtreamChannel(
    string Id,
    string Name,
    string? CategoryId,
    string? StreamType,
    string? ContainerExtension,
    string? LogoUrl = null,
    string? EpgChannelId = null);
