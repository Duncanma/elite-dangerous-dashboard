namespace EliteDashboard.Data.Entities;

/// <summary>
/// Progress is tracked by matching live MarketBuy/MarketSell events (by
/// station + commodity) against the plan's next incomplete hop.
/// </summary>
public sealed class TradeRouteHop
{
    public long Id { get; set; }
    public required long TradeRoutePlanId { get; set; }
    public TradeRoutePlan? Plan { get; set; }

    public int SequenceNumber { get; set; }
    public required long OriginStationId { get; set; }
    public required long DestinationStationId { get; set; }
    public required long CommodityId { get; set; }

    public long PlannedBuyPrice { get; set; }
    public long PlannedSellPrice { get; set; }
    public int PlannedQuantity { get; set; }
    public long PlannedProfit { get; set; }

    public DateTime? BoughtUtc { get; set; }
    public DateTime? SoldUtc { get; set; }
}
