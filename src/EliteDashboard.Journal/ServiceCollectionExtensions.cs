using EliteDashboard.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EliteDashboard.Journal;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJournalIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JournalOptions>(configuration.GetSection(JournalOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JournalOptions>>().Value);
        services.AddSingleton<StateStore>();
        services.AddSingleton<TransactionFeed>();
        services.AddSingleton<JournalTailer>();
        services.AddSingleton<JournalStateReconciler>();
        services.AddSingleton<JournalBackfillService>();
        services.AddHostedService<JournalIngestionService>();
        return services;
    }
}
