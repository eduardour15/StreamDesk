namespace StreamDesk.Probe.Tests;

/// <summary>WPF and native VLC tests share process-wide UI/runtime state and must not overlap other test collections.</summary>
[CollectionDefinition("NativePlayback", DisableParallelization = true)]
public sealed class NativePlaybackCollectionDefinition { }
