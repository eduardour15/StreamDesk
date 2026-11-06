using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;

namespace StreamDesk.Probe.Tests;

public sealed class MultiMonitorAndPlaybackFixTests
{
    [Fact]
    public void DisplayChoice_DefaultsToCurrentMonitor()
    {
        var choices = WindowDisplays.Enumerate();
        Assert.NotEmpty(choices);
        Assert.Equal(0, choices[0].Handle);
        Assert.Equal("Pantalla actual (predeterminada)", choices[0].Name);
    }

    [Fact]
    public async Task TrackSelection_UpdatesThroughViewModel()
    {
        using var db = new TestDatabase();
        var backend = new ControlledBackend();
        var playback = new PlaybackService(backend);
        var credentials = new FixtureCredentials();
        await using var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(2)), playback,
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials);

        await vm.InitializePlaybackAsync();
        await vm.ConnectAsync("https://provider.invalid", "fixture-user", "fixture-password");
        await vm.LoadCatalogAsync();
        vm.SelectedChannel = vm.Channels[0];
        await vm.PlaySelectedAsync();

        var audioTracks = new[] { new MediaTrack(1, "Español"), new MediaTrack(2, "English") };
        var subtitleTracks = new[] { new MediaTrack(10, "Español subs"), new MediaTrack(20, "English subs") };
        backend.Details = new(0, 50000, true, audioTracks, subtitleTracks, 1, 10);

        await vm.RefreshPlaybackDetailsAsync();

        Assert.Equal(2, vm.AudioTracks.Count);
        Assert.Equal(2, vm.SubtitleTracks.Count);
        Assert.Equal(1, vm.SelectedAudioTrack?.Id);
        Assert.Equal(10, vm.SelectedSubtitleTrack?.Id);

        vm.SelectedAudioTrack = vm.AudioTracks[1]; // English
        await vm.SelectTrackAsync(false);
        Assert.Equal((false, 2), backend.ChosenTrack);

        vm.SelectedSubtitleTrack = vm.SubtitleTracks[1]; // English subs
        await vm.SelectTrackAsync(true);
        Assert.Equal((true, 20), backend.ChosenTrack);
    }
}
