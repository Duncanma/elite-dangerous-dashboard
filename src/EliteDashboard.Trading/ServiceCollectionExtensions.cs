using Microsoft.Extensions.DependencyInjection;

namespace EliteDashboard.Trading;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrading(this IServiceCollection services)
    {
        services.AddSingleton<EddnListener>();
        services.AddHostedService(sp => sp.GetRequiredService<EddnListener>());
        services.AddSingleton<TradeRouteEngine>();
        services.AddSingleton<TradeRoutePlanService>();
        return services;
    }
}
