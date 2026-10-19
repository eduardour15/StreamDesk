using System.Net;
using System.Text;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;
using StreamDesk.Probe.Playback;

namespace StreamDesk.Probe.Tests;

public sealed class M3uPlaylistImporterTests
{
    private static readonly IPAddress PublicAddress = IPAddress.Parse("93.184.216.34");

    [Fact]
    public async Task LocalPlaylistParsesUtf8NamesCategoriesAndKeepsStreamUrlOutOfCatalog()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        var path = Path.Combine(db.Root, "canales.m3u8");
        var secret = "fixture-token";
        var contents = "#EXTM3U\r\n#EXTINF:-1 tvg-id=\"noticias.1\" group-title=\"Niñez, Deportes\" tvg-logo=\"https://public.example/logo.png\",Música, Noticias\r\nhttps://public.example/live/1.m3u8?token=" + secret + "\r\n";
        await File.WriteAllTextAsync(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        using var client = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("Network is not expected.")));
        using var importer = new M3uPlaylistImporter(client, (_, _) => Task.FromResult(new[] { PublicAddress }));
        var result = await importer.ImportLocalAsync(path);

        var channel = Assert.Single(result.Catalog.Channels);
        Assert.Equal("Música, Noticias", channel.Name);
        Assert.Null(channel.EpgChannelId);
        Assert.Equal("Niñez, Deportes", Assert.Single(result.Catalog.Categories).Name);
        Assert.Null(channel.LogoUrl); // Imported logos are ignored to avoid a second untrusted network surface.
        Assert.DoesNotContain(secret, channel.Id, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, System.Text.Json.JsonSerializer.Serialize(result.Catalog), StringComparison.Ordinal);
        Assert.Equal(secret, Assert.Single(result.StreamUris).Value.Query.Split("token=")[1]);
    }

    [Fact]
    public async Task RemotePlaylistResolvesRelativeStreamsAndRejectsRedirectsWithoutFollowingThem()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("#EXTM3U\n#EXTINF:-1 group-title=\"Noticias\",Canal\n/live/7.ts?token=fixture\n", Encoding.UTF8)
        }));
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var importer = new M3uPlaylistImporter(client, (_, _) => Task.FromResult(new[] { PublicAddress }));
        var result = await importer.ImportRemoteAsync(new Uri("https://public.example/lists/tv.m3u8?auth=fixture"));
        var stream = Assert.Single(result.StreamUris).Value;
        Assert.Equal("https://public.example/live/7.ts?token=fixture", stream.AbsoluteUri);

        var calls = 0;
        var redirectHandler = new StubHandler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("http://127.0.0.1/private.m3u");
            return Task.FromResult(response);
        });
        using var redirectClient = new HttpClient(redirectHandler) { Timeout = Timeout.InfiniteTimeSpan };
        using var redirectImporter = new M3uPlaylistImporter(redirectClient, (_, _) => Task.FromResult(new[] { PublicAddress }));
        var error = await Assert.ThrowsAsync<PlaylistImportException>(() => redirectImporter.ImportRemoteAsync(new Uri("https://public.example/redirect.m3u")));
        Assert.Equal(1, calls);
        Assert.DoesNotContain("127.0.0.1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemotePlaylistRejectsPrivateDnsAndOversizedResponses()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#EXTM3U") });
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var privateDns = new M3uPlaylistImporter(client, (_, _) => Task.FromResult(new[] { IPAddress.Parse("10.0.0.7") }));
        await Assert.ThrowsAsync<PlaylistImportException>(() => privateDns.ImportRemoteAsync(new Uri("https://playlist.example/list.m3u")));
        Assert.Equal(0, calls);

        var largeHandler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[M3uPlaylistImporter.MaximumPlaylistBytes + 1])
        }));
        using var largeClient = new HttpClient(largeHandler) { Timeout = Timeout.InfiniteTimeSpan };
        using var largeImporter = new M3uPlaylistImporter(largeClient, (_, _) => Task.FromResult(new[] { PublicAddress }));
        await Assert.ThrowsAsync<PlaylistImportException>(() => largeImporter.ImportRemoteAsync(new Uri("https://playlist.example/list.m3u")));

        await Assert.ThrowsAsync<PlaylistImportException>(() => privateDns.EnsureSafeStreamUriAsync(new Uri("https://stream.example/live/1.ts")));
    }

    [Fact]
    public async Task RemoteImportPropagatesCallerCancellationDuringDnsValidation()
    {
        using var client = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("Canceled requests must not reach HTTP.")));
        using var importer = new M3uPlaylistImporter(client,
            (_, token) => Task.FromCanceled<IPAddress[]>(token));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importer.ImportRemoteAsync(new Uri("https://public.example/list.m3u"), cancellation.Token));
    }

    [Fact]
    public async Task RejectsOversizedChannelCountAndUnsafeStreamSchemes()
    {
        using var db = new TestDatabase();
        var path = Path.Combine(db.Root, "demasiados-canales.m3u");
        var text = new StringBuilder("#EXTM3U\n");
        for (var index = 0; index <= M3uPlaylistImporter.MaximumChannels; index++)
            text.Append("#EXTINF:-1,Canal ").Append(index).Append("\nhttps://public.example/live/").Append(index).Append(".ts\n");
        await File.WriteAllTextAsync(path, text.ToString());
        using var client = new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("Network is not expected.")));
        using var importer = new M3uPlaylistImporter(client, (_, _) => Task.FromResult(new[] { PublicAddress }));
        await Assert.ThrowsAsync<PlaylistImportException>(() => importer.ImportLocalAsync(path));

        var unsafePath = Path.Combine(db.Root, "esquemas.m3u");
        await File.WriteAllTextAsync(unsafePath,
            "#EXTM3U\n#EXTINF:-1,Archivo\nfile:///private/video.ts\n#EXTINF:-1,Servidor local\nhttp://127.0.0.1/live.ts\n#EXTINF:-1,Protocolo desconocido\nrtsp://public.example/live\n");
        var error = await Assert.ThrowsAsync<PlaylistImportException>(() => importer.ImportLocalAsync(unsafePath));
        Assert.DoesNotContain("127.0.0.1", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.1")]
    [InlineData("192.168.1.2")]
    [InlineData("169.254.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    public void RejectsNonPublicNetworkAddresses(string value) => Assert.False(M3uPlaylistImporter.IsPublicAddress(IPAddress.Parse(value)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    public void AllowsPublicAddresses(string value) => Assert.True(M3uPlaylistImporter.IsPublicAddress(IPAddress.Parse(value)));

    [Fact]
    public async Task ImportIsSessionOnlyAndPlaybackUsesValidatedPlaylistStreamWithoutXtreamLogin()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        await db.AddProfileAsync("playlist-profile");
        var credentials = new FixtureCredentials();
        var stream = new Uri("https://public.example/live/1.ts?token=fixture-secret");
        var catalog = new CatalogSnapshot([], [new XtreamChannel("m3u-fixture", "Canal de prueba", null, "live", "ts")], DateTimeOffset.UtcNow);
        var importer = new FixturePlaylistImporter(new ImportedPlaylist(catalog, new Dictionary<string, Uri> { ["m3u-fixture"] = stream }));
        var backend = new ControlledBackend();
        var logger = new FixtureLogger();
        var providerCalls = 0;
        var viewer = new SqliteViewerStore(db.PathName, pooling: false);
        await using var vm = new MainViewModel((_, _, _) => { providerCalls++; return new FixtureProvider(CatalogFixtures.Create(1)); },
            new PlaybackService(backend), new ProfileService(db.Settings, credentials), logger,
            new CatalogService(db.Store), credentials, viewer, playlistImporter: importer);
        await vm.InitializePlaybackAsync();
        await vm.LoadProfilesAsync();
        await vm.ImportRemotePlaylistAsync("https://public.example/list.m3u?source=fixture-secret");
        vm.SelectedChannel = vm.Channels.Single();
        await vm.PlaySelectedAsync();
        await Task.Delay(100);

        Assert.Equal(0, providerCalls);
        Assert.Equal(1, backend.Active);
        Assert.Equal(stream, backend.Opens.Last().Source);
        Assert.Equal(stream, importer.ValidatedUri);
        Assert.Empty(logger.Events);
        Assert.Null(await db.Store.LoadAsync("playlist-profile"));
        Assert.DoesNotContain("fixture-secret", vm.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-secret", vm.PlaylistStatus, StringComparison.Ordinal);
        Assert.Equal(string.Empty, vm.RemotePlaylistUrl);
        Assert.True(vm.HasImportedPlaylist);
        Assert.True(vm.CanFavorite);
        await vm.ToggleFavoriteAsync(vm.SelectedChannel);
        Assert.Contains(vm.SelectedChannel!.Id, vm.FavoriteChannelIds);
        Assert.Empty((await viewer.LoadAsync("playlist-profile")).Favorites);
        Assert.Null(await db.Store.LoadAsync("playlist-profile"));
    }

    private sealed class FixturePlaylistImporter(ImportedPlaylist result) : IPlaylistImporter
    {
        public Uri? ValidatedUri { get; private set; }
        public Task<ImportedPlaylist> ImportLocalAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<ImportedPlaylist> ImportRemoteAsync(Uri source, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task EnsureSafeStreamUriAsync(Uri streamUri, CancellationToken cancellationToken = default)
        { ValidatedUri = streamUri; return Task.CompletedTask; }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
