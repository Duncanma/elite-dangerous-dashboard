using EliteDashboard.Data;
using EliteDashboard.External;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Host.Endpoints;

public static class ColonisationEndpoints
{
    public static void MapColonisationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/systems");

        group.MapPost("/sites/{id:long}/shipping-plan", async (ColonisationShippingPlanner planner, long id, CancellationToken ct) =>
        {
            var plan = await planner.BuildPlanAsync(id, ct);
            return Results.Ok(plan);
        });

        group.MapGet("/sites", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();

            var allSites = await db.ColonisationSites.Include(s => s.Resources).ToListAsync();
            var marketIds = allSites.Select(s => s.MarketId).ToList();
            var stations = await db.Stations.Include(s => s.StarSystem)
                .Where(s => marketIds.Contains(s.MarketId))
                .ToDictionaryAsync(s => s.MarketId);

            var deliveriesByMarket = await db.ColonisationDeliveries.AsNoTracking()
                .Where(d => marketIds.Contains(d.MarketId))
                .OrderByDescending(d => d.TimestampUtc)
                .ToListAsync();
            var deliveriesLookup = deliveriesByMarket.ToLookup(d => d.MarketId);

            var claimedSystems = await db.ColonisationSystemClaims
                .Where(c => !c.Released)
                .Select(c => c.StarSystem)
                .ToListAsync();
            var claimedSystemSet = new HashSet<string>(claimedSystems, StringComparer.OrdinalIgnoreCase);

            // ConstructionDepot alone fires for any site with the
            // contribution service you've merely docked at, even with zero
            // involvement - a site only belongs here if you've actually
            // delivered cargo, OR you're the one who claimed its system for
            // colonisation (so it shows up from day one, before your first
            // delivery even lands).
            var sites = allSites.Where(s =>
            {
                if (s.HasContributed)
                {
                    return true;
                }

                var systemName = stations.TryGetValue(s.MarketId, out var st) ? st.StarSystem?.Name : null;
                return systemName is not null && claimedSystemSet.Contains(systemName);
            });

            var dto = sites.Select(s =>
            {
                stations.TryGetValue(s.MarketId, out var station);
                var deliveries = deliveriesLookup[s.MarketId];
                return new
                {
                    s.Id,
                    s.MarketId,
                    StationName = station?.Name ?? "Unknown site",
                    SystemName = station?.StarSystem?.Name ?? "Unknown system",
                    s.ConstructionProgress,
                    s.ConstructionComplete,
                    s.ConstructionFailed,
                    s.FirstSeenUtc,
                    s.LastUpdatedUtc,
                    Resources = s.Resources.Select(r => new
                    {
                        r.CommodityName,
                        r.CommodityDisplayName,
                        r.RequiredAmount,
                        r.ProvidedAmount,
                        r.Payment,
                    }),
                    Deliveries = deliveries.Select(d => new
                    {
                        d.TimestampUtc,
                        d.CommodityName,
                        d.CommodityDisplayName,
                        d.Amount,
                    }),
                    TotalDeliveredTons = deliveries.Sum(d => d.Amount),
                };
            });

            return Results.Ok(dto);
        });

        group.MapGet("/claims", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var claims = await db.ColonisationSystemClaims.Where(c => !c.Released).ToListAsync();
            return Results.Ok(claims);
        });
    }
}
