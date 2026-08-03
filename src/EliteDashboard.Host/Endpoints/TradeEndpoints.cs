using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Host.Endpoints;

public sealed record TradeSearchRequest(
    long? OriginStationId,
    string? OriginMarketId,
    string? OriginSystemName,
    int CargoCapacity,
    long Credits,
    int MaxHops,
    double ShipJumpRangeLy,
    int MaxJumpsFromOrigin,
    int MaxJumpsBetweenStations,
    int? RequiredShipSizeClass,
    bool IncludeUnknownPadSize,
    bool ExcludePlanetary,
    bool ExcludeFleetCarriers);

public sealed record EddnStatusResponse(bool Connected, DateTime? LastMessageAtUtc, long MessagesReceived, long CommodityMessagesProcessed);

public sealed record TradeSearchDefaults(
    int CargoCapacity,
    long Credits,
    double ShipJumpRangeLy,
    string? OriginSystemName,
    int? RequiredShipSizeClass,
    int MaxHops,
    int MaxJumpsFromOrigin,
    int MaxJumpsBetweenStations,
    bool IncludeUnknownPadSize,
    bool ExcludePlanetary,
    bool ExcludeFleetCarriers);

/// <summary>
/// How much of the galaxy the trade route engine can currently place on the
/// map, out of every system that has at least one priced station. Grows
/// monotonically over time (see SystemCoordinateResolver) - never shrinks.
/// </summary>
public sealed record TradeSystemCoverage(int KnownSystems, int UnresolvedSystems);

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/trade");

        group.MapGet("/status", (EddnListener listener) => Results.Ok(new EddnStatusResponse(
            listener.LastMessageAtUtc is not null && DateTime.UtcNow - listener.LastMessageAtUtc < TimeSpan.FromMinutes(2),
            listener.LastMessageAtUtc,
            listener.MessagesReceived,
            listener.CommodityMessagesProcessed)));

        group.MapGet("/defaults", async (StateStore store, IDbContextFactory<DashboardDbContext> factory) => Results.Ok(await BuildDefaultsAsync(store, factory)));

        group.MapGet("/coverage", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var hasCoordinatesPerSystem = await db.Stations
                .Select(s => s.StarSystemId)
                .Distinct()
                .Join(db.StarSystems, id => id, sys => sys.Id, (_, sys) => sys.HasCoordinates)
                .ToListAsync();

            var known = hasCoordinatesPerSystem.Count(h => h);
            var unresolved = hasCoordinatesPerSystem.Count - known;
            return Results.Ok(new TradeSystemCoverage(known, unresolved));
        });

        group.MapPost("/search", async (TradeRouteEngine engine, IDbContextFactory<DashboardDbContext> factory, TradeSearchRequest request) =>
        {
            var criteria = await BuildCriteriaAsync(factory, request);
            var hops = criteria.OriginStationId is null
                ? await engine.FindSingleHopRoutesAsync(criteria, maxResults: Math.Max(1, request.MaxHops))
                : await engine.FindMultiHopRouteAsync(criteria);

            return Results.Ok(hops);
        });

        group.MapPost("/activate", async (TradeRouteEngine engine, TradeRoutePlanService planService, IDbContextFactory<DashboardDbContext> factory, TradeSearchRequest request) =>
        {
            var criteria = await BuildCriteriaAsync(factory, request);
            var hops = criteria.OriginStationId is null
                ? await engine.FindSingleHopRoutesAsync(criteria, maxResults: Math.Max(1, request.MaxHops))
                : await engine.FindMultiHopRouteAsync(criteria);

            if (hops.Count == 0)
            {
                return Results.BadRequest(new { error = "No profitable route found matching the current search criteria." });
            }

            var plan = await planService.ActivateNewPlanAsync(hops, request.Credits);
            var dto = await BuildPlanDtoAsync(factory, plan.Id);
            return Results.Ok(dto);
        });

        group.MapGet("/active", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var plan = await db.TradeRoutePlans.FirstOrDefaultAsync(p => p.Status == TradeRoutePlanStatus.Active);
            if (plan is null)
            {
                return Results.Ok((object?)null);
            }

            return Results.Ok(await BuildPlanDtoAsync(factory, plan.Id));
        });

        group.MapPost("/abandon", async (TradeRoutePlanService planService) =>
        {
            await planService.AbandonActivePlanAsync();
            return Results.NoContent();
        });
    }

    private static async Task<long?> ResolveOriginStationIdAsync(IDbContextFactory<DashboardDbContext> factory, long? originStationId, string? originMarketId)
    {
        if (originStationId is not null)
        {
            return originStationId;
        }

        if (originMarketId is not null && long.TryParse(originMarketId, out var marketId))
        {
            await using var db = await factory.CreateDbContextAsync();
            var station = await db.Stations.FirstOrDefaultAsync(s => s.MarketId == marketId);
            return station?.Id;
        }

        return null;
    }

    private static async Task<TradeRouteSearchCriteria> BuildCriteriaAsync(IDbContextFactory<DashboardDbContext> factory, TradeSearchRequest request)
    {
        var originStationId = await ResolveOriginStationIdAsync(factory, request.OriginStationId, request.OriginMarketId);

        return new TradeRouteSearchCriteria
        {
            OriginStationId = originStationId,
            OriginSystemName = request.OriginSystemName,
            CargoCapacity = request.CargoCapacity,
            Credits = request.Credits,
            MaxHops = Math.Max(1, request.MaxHops),
            ShipJumpRangeLy = request.ShipJumpRangeLy,
            MaxJumpsFromOrigin = request.MaxJumpsFromOrigin,
            MaxJumpsBetweenStations = request.MaxJumpsBetweenStations,
            RequiredShipSizeClass = request.RequiredShipSizeClass,
            IncludeUnknownPadSize = request.IncludeUnknownPadSize,
            ExcludePlanetary = request.ExcludePlanetary,
            ExcludeFleetCarriers = request.ExcludeFleetCarriers,
        };
    }

    // Ship size class is looked up case-insensitively - Frontier's own
    // journal is inconsistent about symbol casing between event types (see
    // the same caveat on FleetEndpoints' /specs).
    private static async Task<TradeSearchDefaults> BuildDefaultsAsync(StateStore store, IDbContextFactory<DashboardDbContext> factory)
    {
        var state = store.Current;

        int? sizeClass = null;
        if (state.ShipType is not null)
        {
            await using var db = await factory.CreateDbContextAsync();
            var specs = await db.ShipStaticSpecs.AsNoTracking().ToListAsync();
            sizeClass = specs.FirstOrDefault(s => string.Equals(s.Symbol, state.ShipType, StringComparison.OrdinalIgnoreCase))?.SizeClass;
        }

        return new TradeSearchDefaults(
            CargoCapacity: (int)state.CargoCapacity,
            Credits: state.Economy.Credits,
            ShipJumpRangeLy: state.MaxJumpRange,
            OriginSystemName: state.CurrentSystem,
            RequiredShipSizeClass: sizeClass,
            MaxHops: 3,
            MaxJumpsFromOrigin: 5,
            MaxJumpsBetweenStations: 2,
            IncludeUnknownPadSize: false,
            ExcludePlanetary: true,
            ExcludeFleetCarriers: true);
    }

    private static async Task<object> BuildPlanDtoAsync(IDbContextFactory<DashboardDbContext> factory, long planId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var plan = await db.TradeRoutePlans.Include(p => p.Hops).FirstAsync(p => p.Id == planId);

        var stationIds = plan.Hops.SelectMany(h => new[] { h.OriginStationId, h.DestinationStationId }).Distinct().ToList();
        var stations = await db.Stations.Include(s => s.StarSystem).Where(s => stationIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var commodityIds = plan.Hops.Select(h => h.CommodityId).Distinct().ToList();
        var commodities = await db.Commodities.Where(c => commodityIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        return new
        {
            plan.Id,
            plan.Status,
            plan.CreatedUtc,
            plan.StartingCapital,
            plan.TotalHops,
            plan.EstimatedProfit,
            plan.ActivatedUtc,
            plan.CompletedUtc,
            Hops = plan.Hops.OrderBy(h => h.SequenceNumber).Select(h => new
            {
                h.Id,
                h.SequenceNumber,
                OriginStationName = stations.GetValueOrDefault(h.OriginStationId)?.Name ?? "Unknown",
                OriginSystemName = stations.GetValueOrDefault(h.OriginStationId)?.StarSystem?.Name ?? "Unknown",
                OriginStationType = stations.GetValueOrDefault(h.OriginStationId)?.StationType,
                DestinationStationName = stations.GetValueOrDefault(h.DestinationStationId)?.Name ?? "Unknown",
                DestinationSystemName = stations.GetValueOrDefault(h.DestinationStationId)?.StarSystem?.Name ?? "Unknown",
                DestinationStationType = stations.GetValueOrDefault(h.DestinationStationId)?.StationType,
                CommodityName = commodities.GetValueOrDefault(h.CommodityId)?.DisplayName ?? "Unknown",
                h.PlannedBuyPrice,
                h.PlannedSellPrice,
                h.PlannedQuantity,
                h.PlannedProfit,
                h.BoughtUtc,
                h.SoldUtc,
            }),
        };
    }
}
