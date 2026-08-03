using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Trading;

public sealed record TradeRoutePlanProgress(long PlanId, TradeRoutePlanStatus Status, int CompletedHops, int TotalHops);

/// <summary>
/// Owns activate/track/recall for trade route plans. Progress is tracked by
/// matching live MarketBuy/MarketSell journal events (by station + commodity)
/// against the active plan's next incomplete hop - called directly from
/// JournalStateReconciler, the same place Economics transactions are recorded.
/// </summary>
public sealed class TradeRoutePlanService
{
    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;

    public event Action<TradeRoutePlanProgress>? ProgressChanged;

    public TradeRoutePlanService(IDbContextFactory<DashboardDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<TradeRoutePlan> ActivateNewPlanAsync(IReadOnlyList<TradeRouteCandidate> candidates, long startingCapital, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var existingActive = await db.TradeRoutePlans.Where(p => p.Status == TradeRoutePlanStatus.Active).ToListAsync(ct);
        foreach (var old in existingActive)
        {
            old.Status = TradeRoutePlanStatus.Abandoned;
        }

        var plan = new TradeRoutePlan
        {
            CreatedUtc = DateTime.UtcNow,
            Engine = "Local",
            Status = TradeRoutePlanStatus.Active,
            StartingCapital = startingCapital,
            TotalHops = candidates.Count,
            EstimatedProfit = candidates.Sum(c => c.TotalProfit),
            ActivatedUtc = DateTime.UtcNow,
        };

        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            plan.Hops.Add(new TradeRouteHop
            {
                TradeRoutePlanId = 0,
                SequenceNumber = i,
                OriginStationId = c.OriginStationId,
                DestinationStationId = c.DestinationStationId,
                CommodityId = c.CommodityId,
                PlannedBuyPrice = c.BuyPrice,
                PlannedSellPrice = c.SellPrice,
                PlannedQuantity = c.Quantity,
                PlannedProfit = c.TotalProfit,
            });
        }

        db.TradeRoutePlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<TradeRoutePlan?> GetActivePlanAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return await db.TradeRoutePlans
            .Include(p => p.Hops)
            .FirstOrDefaultAsync(p => p.Status == TradeRoutePlanStatus.Active, ct);
    }

    public async Task AbandonActivePlanAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var plan = await db.TradeRoutePlans.FirstOrDefaultAsync(p => p.Status == TradeRoutePlanStatus.Active, ct);
        if (plan is null)
        {
            return;
        }

        plan.Status = TradeRoutePlanStatus.Abandoned;
        await db.SaveChangesAsync(ct);
    }

    public Task HandleMarketBuyAsync(long marketId, string commoditySymbol, CancellationToken ct = default) =>
        HandleMarketEventAsync(marketId, commoditySymbol, isBuy: true, ct);

    public Task HandleMarketSellAsync(long marketId, string commoditySymbol, CancellationToken ct = default) =>
        HandleMarketEventAsync(marketId, commoditySymbol, isBuy: false, ct);

    private async Task HandleMarketEventAsync(long marketId, string commoditySymbol, bool isBuy, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var plan = await db.TradeRoutePlans
            .Include(p => p.Hops)
            .FirstOrDefaultAsync(p => p.Status == TradeRoutePlanStatus.Active, ct);
        if (plan is null)
        {
            return;
        }

        var station = await db.Stations.FirstOrDefaultAsync(s => s.MarketId == marketId, ct);
        var commodity = await db.Commodities.FirstOrDefaultAsync(c => c.InternalSymbol == commoditySymbol, ct);
        if (station is null || commodity is null)
        {
            return;
        }

        var hop = isBuy
            ? plan.Hops.Where(h => h.BoughtUtc == null).OrderBy(h => h.SequenceNumber)
                .FirstOrDefault(h => h.OriginStationId == station.Id && h.CommodityId == commodity.Id)
            : plan.Hops.Where(h => h.BoughtUtc != null && h.SoldUtc == null).OrderBy(h => h.SequenceNumber)
                .FirstOrDefault(h => h.DestinationStationId == station.Id && h.CommodityId == commodity.Id);

        if (hop is null)
        {
            return;
        }

        if (isBuy)
        {
            hop.BoughtUtc = DateTime.UtcNow;
        }
        else
        {
            hop.SoldUtc = DateTime.UtcNow;
        }

        var completedHops = plan.Hops.Count(h => h.SoldUtc != null);
        if (completedHops == plan.Hops.Count)
        {
            plan.Status = TradeRoutePlanStatus.Completed;
            plan.CompletedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        ProgressChanged?.Invoke(new TradeRoutePlanProgress(plan.Id, plan.Status, completedHops, plan.Hops.Count));
    }
}
