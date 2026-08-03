using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.External;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Trading;

/// <summary>
/// Bounded-hop heuristic route finder against the locally cached price data
/// (fed by EddnListener) - deliberately not a provably-optimal multi-hop
/// solver. A single-hop search ranks every profitable (buy, sell) pair for
/// each commodity that survives the station-eligibility and distance
/// filters in <see cref="TradeRouteSearchCriteria"/>; multi-hop greedily
/// chains the best next single hop from the previous destination, up to a
/// hop cap.
/// </summary>
public sealed class TradeRouteEngine
{
    // A full galaxy-wide search can touch thousands of distinct EDDN-known
    // systems. Resolving coordinates for all of them via EDSM would repeat
    // the ColonisationShippingPlanner's original multi-minute-hang bug, so
    // the same bounded/parallel/skip-if-plenty pattern applies here, scoped
    // per search rather than per commodity.
    private const int MinKnownSystemsBeforeSkippingEdsm = 30;
    private const int MaxEdsmLookupsPerSearch = 25;
    private const int MaxConcurrentEdsmLookups = 5;

    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;
    private readonly SystemCoordinateResolver _coordinateResolver;

    public TradeRouteEngine(IDbContextFactory<DashboardDbContext> contextFactory, SystemCoordinateResolver coordinateResolver)
    {
        _contextFactory = contextFactory;
        _coordinateResolver = coordinateResolver;
    }

    public async Task<IReadOnlyList<TradeRouteCandidate>> FindSingleHopRoutesAsync(
        TradeRouteSearchCriteria criteria, int maxResults = 10, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        StarSystem? origin = null;
        if (criteria.OriginStationId is null && criteria.OriginSystemName is not null)
        {
            origin = await db.StarSystems.FirstOrDefaultAsync(s => s.Name == criteria.OriginSystemName, ct);
            if (origin is not null && !origin.HasCoordinates)
            {
                await _coordinateResolver.TryResolveAsync(origin, ct);
            }
        }

        var quotes = await db.MarketPriceQuotes
            .Include(q => q.Station).ThenInclude(s => s!.StarSystem)
            .Include(q => q.Commodity)
            .Where(q => q.Station != null && q.Commodity != null)
            .ToListAsync(ct);

        quotes = quotes.Where(q => IsStationEligible(q.Station!, criteria)).ToList();

        // Bounded EDSM coordinate backfill, scoped to only the systems that
        // survived the station-eligibility filters above.
        var distinctSystems = quotes.Select(q => q.Station!.StarSystem!).DistinctBy(s => s.Id).ToList();
        var knownCount = distinctSystems.Count(s => s.HasCoordinates);
        var unknownSystems = distinctSystems.Where(s => !s.HasCoordinates).ToList();

        if (knownCount < MinKnownSystemsBeforeSkippingEdsm && unknownSystems.Count > 0)
        {
            var toResolve = unknownSystems.Take(MaxEdsmLookupsPerSearch).ToList();
            using var gate = new SemaphoreSlim(MaxConcurrentEdsmLookups);
            await Task.WhenAll(toResolve.Select(async system =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    await _coordinateResolver.TryResolveAsync(system, ct);
                }
                finally
                {
                    gate.Release();
                }
            }));
        }

        // Systems we still don't have coordinates for can't be distance-
        // filtered, so they're dropped rather than guessed at.
        quotes = quotes.Where(q => q.Station!.StarSystem!.HasCoordinates).ToList();

        var originRadiusLy = criteria.MaxJumpsFromOrigin * criteria.ShipJumpRangeLy;
        var hopCapLy = criteria.MaxJumpsBetweenStations * criteria.ShipJumpRangeLy;
        var candidates = new List<TradeRouteCandidate>();

        foreach (var group in quotes.GroupBy(q => q.CommodityId))
        {
            var buyOptions = group.Where(q => q.BuyPrice > 0 && IsEligibleOrigin(q, criteria, origin, originRadiusLy));
            var sellOptions = group.Where(q => q.SellPrice > 0).ToList();

            foreach (var buy in buyOptions)
            {
                foreach (var sell in sellOptions)
                {
                    if (sell.StationId == buy.StationId)
                    {
                        continue;
                    }

                    if (criteria.ShipJumpRangeLy > 0 && hopCapLy > 0
                        && SystemCoordinateResolver.Distance(buy.Station!.StarSystem!, sell.Station!.StarSystem!) > hopCapLy)
                    {
                        continue;
                    }

                    var profitPerUnit = sell.SellPrice - buy.BuyPrice;
                    if (profitPerUnit <= 0)
                    {
                        continue;
                    }

                    var maxAffordable = buy.BuyPrice > 0 ? (int)Math.Min(criteria.Credits / buy.BuyPrice, int.MaxValue) : 0;
                    var quantity = new[] { criteria.CargoCapacity, maxAffordable, (int)Math.Min(buy.Supply, int.MaxValue), (int)Math.Min(sell.Demand, int.MaxValue) }
                        .Where(v => v > 0)
                        .DefaultIfEmpty(0)
                        .Min();

                    if (quantity <= 0)
                    {
                        continue;
                    }

                    candidates.Add(new TradeRouteCandidate
                    {
                        OriginStationId = buy.StationId,
                        OriginStationName = buy.Station!.Name,
                        OriginSystemName = buy.Station.StarSystem?.Name ?? "Unknown",
                        OriginStationType = buy.Station.StationType,
                        DestinationStationId = sell.StationId,
                        DestinationStationName = sell.Station!.Name,
                        DestinationSystemName = sell.Station.StarSystem?.Name ?? "Unknown",
                        DestinationStationType = sell.Station.StationType,
                        CommodityId = buy.CommodityId,
                        CommodityName = buy.Commodity!.DisplayName,
                        BuyPrice = buy.BuyPrice,
                        SellPrice = sell.SellPrice,
                        Quantity = quantity,
                        TotalProfit = profitPerUnit * quantity,
                    });
                }
            }
        }

        return candidates.OrderByDescending(c => c.TotalProfit).Take(maxResults).ToList();
    }

    public async Task<IReadOnlyList<TradeRouteCandidate>> FindMultiHopRouteAsync(TradeRouteSearchCriteria criteria, CancellationToken ct = default)
    {
        var hops = new List<TradeRouteCandidate>();
        var currentCriteria = criteria;

        for (var i = 0; i < Math.Max(1, criteria.MaxHops); i++)
        {
            var options = await FindSingleHopRoutesAsync(currentCriteria, maxResults: 1, ct);
            if (options.Count == 0)
            {
                break;
            }

            var best = options[0];
            hops.Add(best);
            currentCriteria = currentCriteria with { OriginStationId = best.DestinationStationId, Credits = currentCriteria.Credits + best.TotalProfit };
        }

        return hops;
    }

    private static bool IsStationEligible(Station station, TradeRouteSearchCriteria criteria)
    {
        if (criteria.ExcludePlanetary && StationCapabilities.IsPlanetary(station))
        {
            return false;
        }

        if (criteria.ExcludeFleetCarriers && StationCapabilities.IsFleetCarrier(station))
        {
            return false;
        }

        if (criteria.RequiredShipSizeClass is int requiredSize)
        {
            var supports = StationCapabilities.SupportsShipSize(station, requiredSize);
            if (supports == false || (supports is null && !criteria.IncludeUnknownPadSize))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEligibleOrigin(MarketPriceQuote quote, TradeRouteSearchCriteria criteria, StarSystem? origin, double originRadiusLy)
    {
        if (criteria.OriginStationId is not null)
        {
            return quote.StationId == criteria.OriginStationId;
        }

        if (origin is null || criteria.ShipJumpRangeLy <= 0)
        {
            return true;
        }

        return SystemCoordinateResolver.Distance(origin, quote.Station!.StarSystem!) <= originRadiusLy;
    }
}
