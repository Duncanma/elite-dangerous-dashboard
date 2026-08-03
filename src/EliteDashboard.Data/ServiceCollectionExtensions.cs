using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EliteDashboard.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Dashboard") ?? DefaultConnectionString();

        services.AddDbContextFactory<DashboardDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<DbWriteQueue>();
        services.AddHostedService<DbWriterService>();

        return services;
    }

    /// <summary>
    /// Applies pending migrations and enables WAL mode (concurrent readers +
    /// a single writer, which is exactly the DbWriteQueue's access pattern).
    /// Call once at startup before anything else touches the database.
    /// </summary>
    public static async Task MigrateDashboardDataAsync(this IServiceProvider services)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<DashboardDbContext>>().CreateDbContextAsync();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }

    /// <summary>
    /// Stores the DB under LocalApplicationData rather than next to the app
    /// binaries, so it survives app updates/reinstalls.
    /// </summary>
    private static string DefaultConnectionString()
    {
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EliteDashboard");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "dashboard.db");
        return $"Data Source={dbPath}";
    }
}
