using Microsoft.Extensions.DependencyInjection;

namespace EliteDashboard.External;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExternalIntegrations(this IServiceCollection services)
    {
        // GitHub's API rejects requests with no User-Agent header, and
        // HttpClient doesn't send one by default.
        services.AddHttpClient(nameof(ReferenceDataImporter), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EliteDashboard/1.0 (personal-use)");
        });
        services.AddHttpClient();
        services.AddSingleton<ReferenceDataImporter>();
        services.AddHostedService<ReferenceDataStartupService>();
        services.AddSingleton<InaraClient>();
        services.AddHostedService<InaraSyncService>();

        services.AddHttpClient(nameof(EdsmClient), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EliteDashboard/1.0 (personal-use)");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<EdsmClient>();
        services.AddSingleton<SystemCoordinateResolver>();
        services.AddSingleton<ColonisationShippingPlanner>();

        // Station.csv is ~110MB - the default 100s HttpClient timeout isn't
        // enough headroom on a slow connection.
        services.AddHttpClient(nameof(GalaxyDataImporter), client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EliteDashboard/1.0 (personal-use)");
            client.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddSingleton<GalaxyDataImporter>();
        services.AddHostedService<GalaxyDataStartupService>();

        return services;
    }
}
