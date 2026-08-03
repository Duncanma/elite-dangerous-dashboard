using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.External;

public sealed record ShippingPlanItem(string CommodityName, long Quantity);

public sealed record ShippingPlanStop(string StationName, string SystemName, string? StationType, double DistanceLy, IReadOnlyList<ShippingPlanItem> Items);

public sealed record ShippingPlanResult(bool Success, string? Message, IReadOnlyList<ShippingPlanStop> Stops, IReadOnlyList<string> UnresolvedCommodities);

/// <summary>
/// Given a colonisation site's outstanding materials, finds the nearest
/// stations currently selling each one (from our own EDDN-fed market cache)
/// and groups them into an ordered shopping route. Deliberately a greedy
/// nearest-per-commodity heuristic, not a true multi-stop route optimizer -
/// same "bounded heuristic over optimal solver" tradeoff as TradeRouteEngine.
/// </summary>
public sealed class ColonisationShippingPlanner
{
    private static readonly TimeSpan MaxQuoteAge = TimeSpan.FromDays(3);

    // A common commodity can have hundreds of galaxy-wide EDDN listings.
    // Systems the commander has actually flown through already have free,
    // cached coordinates (from FSDJump); only fall back to EDSM for a
    // bounded, parallelized batch of the rest, and skip it entirely if
    // there are already plenty of free candidates to choose from.
    private const int MinKnownCandidatesBeforeSkippingEdsm = 20;
    private const int MaxEdsmLookupsPerCommodity = 12;
    private const int MaxConcurrentEdsmLookups = 5;

    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;
    private readonly SystemCoordinateResolver _coordinateResolver;

    public ColonisationShippingPlanner(IDbContextFactory<DashboardDbContext> contextFactory, SystemCoordinateResolver coordinateResolver)
    {
        _contextFactory = contextFactory;
        _coordinateResolver = coordinateResolver;
    }

    public async Task<ShippingPlanResult> BuildPlanAsync(long siteId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var site = await db.ColonisationSites.Include(s => s.Resources).FirstOrDefaultAsync(s => s.Id == siteId, ct);
        if (site is null)
        {
            return Fail("Site not found.");
        }

        var originStation = await db.Stations.Include(s => s.StarSystem).FirstOrDefaultAsync(s => s.MarketId == site.MarketId, ct);
        var origin = originStation?.StarSystem;
        if (origin is null)
        {
            return Fail("This site's system isn't known yet - dock there at least once first.");
        }

        if (!origin.HasCoordinates && !await _coordinateResolver.TryResolveAsync(origin, ct))
        {
            return Fail($"Could not determine {origin.Name}'s galactic position.");
        }

        var outstanding = site.Resources.Where(r => r.ProvidedAmount < r.RequiredAmount).ToList();
        if (outstanding.Count == 0)
        {
            return Fail("No outstanding materials - this site is fully supplied.");
        }

        var unresolved = new List<string>();
        var stopsByStationId = new Dictionary<long, (Station Station, double Distance, List<ShippingPlanItem> Items)>();

        foreach (var resource in outstanding)
        {
            var remaining = resource.RequiredAmount - resource.ProvidedAmount;
            var displayName = resource.CommodityDisplayName ?? resource.CommodityName;
            var normalizedSymbol = NormalizeCommodityToken(resource.CommodityName);

            var commodityIds = await db.Commodities
                .Where(c => c.InternalSymbol.ToLower() == normalizedSymbol)
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (commodityIds.Count == 0)
            {
                unresolved.Add(displayName);
                continue;
            }

            var cutoff = DateTime.UtcNow - MaxQuoteAge;
            var quotes = await db.MarketPriceQuotes
                .Include(q => q.Station).ThenInclude(s => s!.StarSystem)
                .Where(q => commodityIds.Contains(q.CommodityId) && q.BuyPrice > 0 && q.Supply > 0 && q.CollectedAtUtc >= cutoff)
                .Where(q => q.Station != null && q.Station.StarSystem != null && q.StationId != originStation!.Id)
                .ToListAsync(ct);

            if (quotes.Count == 0)
            {
                unresolved.Add(displayName);
                continue;
            }

            var candidates = new List<(MarketPriceQuote Quote, double Distance, bool IsPlanetary)>();
            var unknownSystems = new Dictionary<string, StarSystem>();
            var unknownQuotesBySystem = new Dictionary<string, List<MarketPriceQuote>>();

            foreach (var quote in quotes)
            {
                var system = quote.Station!.StarSystem!;
                if (system.HasCoordinates)
                {
                    candidates.Add((quote, SystemCoordinateResolver.Distance(origin, system), StationCapabilities.IsPlanetary(quote.Station)));
                    continue;
                }

                unknownSystems[system.Name] = system;
                if (!unknownQuotesBySystem.TryGetValue(system.Name, out var list))
                {
                    list = [];
                    unknownQuotesBySystem[system.Name] = list;
                }

                list.Add(quote);
            }

            if (candidates.Count < MinKnownCandidatesBeforeSkippingEdsm && unknownSystems.Count > 0)
            {
                var toResolve = unknownSystems.Values.Take(MaxEdsmLookupsPerCommodity).ToList();
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

                foreach (var system in toResolve.Where(s => s.HasCoordinates))
                {
                    var distance = SystemCoordinateResolver.Distance(origin, system);
                    foreach (var quote in unknownQuotesBySystem[system.Name])
                    {
                        candidates.Add((quote, distance, StationCapabilities.IsPlanetary(quote.Station!)));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                unresolved.Add(displayName);
                continue;
            }

            var pool = candidates.Where(c => !c.IsPlanetary).ToList();
            if (pool.Count == 0)
            {
                pool = candidates;
            }

            var best = pool.OrderBy(c => c.Distance).First();
            var bestStation = best.Quote.Station!;

            if (!stopsByStationId.TryGetValue(bestStation.Id, out var stop))
            {
                stop = (bestStation, best.Distance, []);
                stopsByStationId[bestStation.Id] = stop;
            }

            stop.Items.Add(new ShippingPlanItem(displayName, remaining));
        }

        var stops = stopsByStationId.Values
            .OrderBy(s => s.Distance)
            .Select(s => new ShippingPlanStop(s.Station.Name, s.Station.StarSystem!.Name, s.Station.StationType, Math.Round(s.Distance, 1), s.Items))
            .ToList();

        if (stops.Count == 0)
        {
            return Fail("No current suppliers found for any outstanding material within recent EDDN data.");
        }

        return new ShippingPlanResult(true, null, stops, unresolved);
    }

    /// <summary>"$aluminium_name;" -&gt; "aluminium" (also handles EDDN's inconsistent casing).</summary>
    private static string NormalizeCommodityToken(string rawName)
    {
        var name = rawName;
        if (name.StartsWith('$'))
        {
            name = name[1..];
        }

        if (name.EndsWith(';'))
        {
            name = name[..^1];
        }

        const string suffix = "_name";
        if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^suffix.Length];
        }

        return name.ToLowerInvariant();
    }

    private static ShippingPlanResult Fail(string message) => new(false, message, [], []);
}
