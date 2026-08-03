namespace EliteDashboard.Data.Entities;

/// <summary>
/// Latest-only per (StationId, CommodityId) - unbounded EDDN history isn't
/// kept in the MVP, since the firehose volume makes that a real disk/index
/// growth risk for no near-term payoff.
/// </summary>
public sealed class MarketPriceQuote
{
    public long Id { get; set; }
    public required long StationId { get; set; }
    public Station? Station { get; set; }
    public required long CommodityId { get; set; }
    public Commodity? Commodity { get; set; }
    public long BuyPrice { get; set; }
    public long SellPrice { get; set; }
    public long Demand { get; set; }
    public long Supply { get; set; }
    public DateTime CollectedAtUtc { get; set; }
    public string Source { get; set; } = "EDDN";
}
