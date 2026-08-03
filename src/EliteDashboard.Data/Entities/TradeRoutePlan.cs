namespace EliteDashboard.Data.Entities;

public enum TradeRoutePlanStatus
{
    Draft,
    Active,
    Completed,
    Abandoned,
}

/// <summary>Exactly one Active row at a time - this is "the current trade plan you can recall."</summary>
public sealed class TradeRoutePlan
{
    public long Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Engine { get; set; } = "Local";
    public TradeRoutePlanStatus Status { get; set; } = TradeRoutePlanStatus.Draft;
    public long StartingCapital { get; set; }
    public int TotalHops { get; set; }
    public long EstimatedProfit { get; set; }
    public DateTime? ActivatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    public List<TradeRouteHop> Hops { get; set; } = [];
}
