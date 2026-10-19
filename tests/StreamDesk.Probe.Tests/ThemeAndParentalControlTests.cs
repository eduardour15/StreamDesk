using Microsoft.Data.Sqlite;
using StreamDesk.Application;
using StreamDesk.Probe.Core;
using StreamDesk.Probe.Infrastructure;
using Xunit;

namespace StreamDesk.Probe.Tests;

public sealed class ThemeAndParentalControlTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "streamdesk-parental-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task AppSettingsPersistsThemeScaleAndLanguage()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "settings.db");
            var settingsService = new SqliteSettingsService(dbPath, pooling: false);
            await settingsService.InitializeAsync();

            var initial = await settingsService.GetAppSettingsAsync();
            Assert.Equal("Dark", initial.Theme);
            Assert.Equal("100%", initial.UiScale);
            Assert.Equal("es", initial.Language);
            Assert.False(initial.ParentalControlEnabled);

            var updated = initial with
            {
                Theme = "Light",
                UiScale = "115%",
                Language = "en"
            };
            await settingsService.SaveAppSettingsAsync(updated);

            var loaded = await settingsService.GetAppSettingsAsync();
            Assert.Equal("Light", loaded.Theme);
            Assert.Equal("115%", loaded.UiScale);
            Assert.Equal("en", loaded.Language);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ParentalControlPinConfigurationAndVerification()
    {
        var root = CreateTempDirectory();
        try
        {
            var dbPath = Path.Combine(root, "parental.db");
            var settingsService = new SqliteSettingsService(dbPath, pooling: false);
            await settingsService.InitializeAsync();

            var parental = new ParentalControlService(settingsService);
            await parental.InitializeAsync();

            Assert.False(parental.IsEnabled);
            Assert.False(parental.IsLocked);

            // Configure PIN 1234
            await parental.ConfigurePinAsync("1234");
            Assert.True(parental.IsEnabled);
            Assert.True(parental.IsLocked);

            // Cannot unlock with wrong PIN
            Assert.False(parental.Unlock("9999"));
            Assert.True(parental.IsLocked);

            // Unlocks with correct PIN
            Assert.True(parental.Unlock("1234"));
            Assert.False(parental.IsLocked);

            // Re-lock
            parental.Lock();
            Assert.True(parental.IsLocked);

            // Disable with correct PIN
            Assert.False(await parental.DisableAsync("0000"));
            Assert.True(await parental.DisableAsync("1234"));
            Assert.False(parental.IsEnabled);
            Assert.False(parental.IsLocked);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("Canales Adultos", true)]
    [InlineData("Canal XXX Plus", true)]
    [InlineData("Cine 18+", true)]
    [InlineData("Erotic HD", true)]
    [InlineData("Deportes en Vivo", false)]
    [InlineData("Noticias 24h", false)]
    [InlineData("Infantil / Dibujos", false)]
    public void IdentifiesRestrictedAdultCategories(string categoryName, bool shouldBeRestricted)
    {
        var service = new ParentalControlService(new DummySettingsService());
        var isRestricted = service.IsRestrictedCategory(categoryName);
        Assert.Equal(shouldBeRestricted, isRestricted);
    }

    private sealed class DummySettingsService : ISettingsService
    {
        private AppSettings _settings = new();
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Profile>> GetProfilesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Profile>>([]);
        public Task AddProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AppSettings> GetAppSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task SaveAppSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) { _settings = settings; return Task.CompletedTask; }
    }
}
