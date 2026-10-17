using Microsoft.Extensions.DependencyInjection;

namespace StreamDesk.Probe.Infrastructure;

public static class ProviderHttpClients
{
    public static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        Configure(services);
        return services.BuildServiceProvider();
    }

    public static void Configure(IServiceCollection services)
    {
        foreach (var name in new[] { "Xtream", "Logos" })
        {
            services.AddHttpClient(name, client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    MaxConnectionsPerServer = 4,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                })
                .RemoveAllLoggers();
        }
    }
}
