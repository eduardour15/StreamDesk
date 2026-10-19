using System.Net;
using System.Text;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Desktop;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class AuditRegressionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedProfileDeletionPreservesCredentialsCatalogFavoritesAndHistory(bool credentialFailure)
    {
        using var db = new TestDatabase(); await db.AddProfileAsync("first");
        await db.Store.ReplaceAsync("first", CatalogFixtures.Create(10));
        db.Execute("INSERT INTO Favorites VALUES('first','live','000003','2026-10-17T12:00:00Z'); INSERT INTO PlaybackHistory(ProfileId,ContentType,ChannelId,PlayedAt) VALUES('first','live','000003','2026-10-17T12:00:00Z');");
        var profile = Assert.Single(await db.Settings.GetProfilesAsync());
        var credentials = new AuditCredentials { FailDelete = credentialFailure };
        var secret = new StoredCredentials("fixture-user", "fixture-password");
        await credentials.SaveAsync(profile.CredentialKey, secret);
        if (!credentialFailure) db.Execute("CREATE TRIGGER FailDelete BEFORE DELETE ON Profiles BEGIN SELECT RAISE(ABORT,'fixture failure'); END;");

        await Assert.ThrowsAnyAsync<Exception>(() => new ProfileService(db.Settings, credentials).DeleteAsync("first"));

        Assert.Single(await db.Settings.GetProfilesAsync());
        Assert.Equal(secret, await credentials.GetAsync(profile.CredentialKey));
        var catalog = Assert.IsType<CatalogSnapshot>(await db.Store.LoadAsync("first"));
        Assert.Equal(10, catalog.Channels.Count);
        Assert.Contains("000003", catalog.FavoriteChannelIds!);
        using var connection = db.Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PlaybackHistory WHERE ProfileId='first';";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public async Task CancelledDpapiSaveDoesNotCreateAnyFiles()
    {
        using var db = new TestDatabase();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var store = new DpapiCredentialStore(Path.Combine(db.Root, "secrets"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(Guid.NewGuid().ToString("N"), new("fixture-user", "fixture-password"), cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(db.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DisposingProviderCancelsInflightRequestAsCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new XtreamApiClient("https://provider.invalid", "fixture-user", "fixture-password",
            handler: new XtreamPhase2Tests.Handler(async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new(HttpStatusCode.OK);
            }));
        var request = provider.AuthenticateAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        provider.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task LogoLoaderCannotRestartAfterDisposal()
    {
        using var loader = new ChannelLogoLoader(new HttpClient(new XtreamPhase2Tests.Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not an image", Encoding.UTF8) }))));
        loader.Dispose();
        loader.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => loader.GetAsync("https://logos.invalid/logo.png", CancellationToken.None));
        Assert.Equal(0, loader.CachedCount);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task StreamUriPreservesCredentialPathSegments(string username)
    {
        using var provider = new XtreamApiClient("https://provider.invalid/base", username, "fixture-password", handler:
            new XtreamPhase2Tests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("""{"user_info":{"auth":1,"status":"Active","allowed_output_formats":["ts"]}}""") })));
        await provider.AuthenticateAsync();
        var uri = provider.CreateLiveStreamUri(new("42", "Fixture", null, "live", "ts"), "ts");
        Assert.Equal(5, uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.EndsWith("/fixture-password/42.ts", uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileCreationSearchStateAndRapidSwitchDisposalAreDeterministic()
    {
        using var db = new TestDatabase();
        await db.Settings.InitializeAsync();
        var credentials = new AuditCredentials();
        await using (var vm = new MainViewModel((_, _, _) => new FixtureProvider(CatalogFixtures.Create(5)), new FixturePlayback(),
            new ProfileService(db.Settings, credentials), new FixtureLogger(), new CatalogService(db.Store), credentials))
        {
            Assert.False(vm.HasSearchText);
            Assert.False(vm.HasMediaSearch);
            vm.SearchText = "deportes";
            vm.MediaSearch = "matrix";
            Assert.True(vm.HasSearchText);
            Assert.True(vm.HasMediaSearch);
            vm.SearchText = "";
            vm.MediaSearch = "";
            Assert.False(vm.HasSearchText);
            Assert.False(vm.HasMediaSearch);

            Assert.False(await vm.CreateProfileAsync(""));
            vm.ProfileName = "Sala";
            vm.ProfileBaseUrl = "https://provider.invalid";
            vm.ProfileUsername = "fixture-user";
            Assert.True(await vm.CreateProfileAsync("fixture-password"));
            Assert.NotNull(vm.ActiveProfile);
            Assert.Equal(vm.ActiveProfile?.Id, vm.SelectedProfile?.Id);
        }
    }
}

internal sealed class AuditCredentials : ICredentialStore
{
    private readonly Dictionary<string, StoredCredentials> _values = [];
    public bool FailDelete { get; set; }
    public Task SaveAsync(string key, StoredCredentials credentials, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); _values[key] = credentials; return Task.CompletedTask; }
    public Task<StoredCredentials?> GetAsync(string key, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_values.GetValueOrDefault(key)); }
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailDelete) throw new IOException("Fixture credential failure.");
        _values.Remove(key); return Task.CompletedTask;
    }
}
