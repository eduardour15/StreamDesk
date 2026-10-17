using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamDesk.Probe.Infrastructure;

namespace StreamDesk.Probe.Tests;

public sealed class HttpFactoryPrivacyTests
{
    [Fact]
    public async Task FactoryDoesNotLogCredentialBearingRequestUrlsEvenWithLoggingProviderInstalled()
    {
        var spy = new SpyLogger();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(spy));
        ProviderHttpClients.Configure(services);
        services.AddHttpClient("Xtream").ConfigurePrimaryHttpMessageHandler(() => new XtreamPhase2Tests.Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK))));
        using var container = services.BuildServiceProvider();
        using var client = container.GetRequiredService<IHttpClientFactory>().CreateClient("Xtream");
        using var response = await client.GetAsync("https://provider.invalid/player_api.php?username=fixture-user&password=fixture-password");
        Assert.DoesNotContain(spy.Messages, message => message.Contains("fixture-password", StringComparison.Ordinal));
        Assert.DoesNotContain(spy.Messages, message => message.Contains("player_api.php", StringComparison.Ordinal));
    }

    private sealed class SpyLogger : ILoggerProvider, ILogger
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        public void Dispose() { }
    }
}
