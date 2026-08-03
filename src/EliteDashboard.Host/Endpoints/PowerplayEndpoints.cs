using EliteDashboard.Data;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Host.Endpoints;

public static class PowerplayEndpoints
{
    public static void MapPowerplayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/powerplay");

        group.MapGet("/standing", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var standing = await db.PowerplayStandings.AsNoTracking().FirstOrDefaultAsync();
            return Results.Ok(standing);
        });

        group.MapGet("/history", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var history = await db.PowerplayStandingHistories.AsNoTracking()
                .OrderBy(h => h.TimestampUtc)
                .ToListAsync();
            return Results.Ok(history);
        });
    }
}
