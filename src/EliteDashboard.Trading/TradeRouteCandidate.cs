namespace EliteDashboard.Trading;

public sealed record TradeRouteCandidate
{
    public required long OriginStationId { get; init; }
    public required string OriginStationName { get; init; }
    public required string OriginSystemName { get; init; }
    public string? OriginStationType { get; init; }
    public required long DestinationStationId { get; init; }
    public required string DestinationStationName { get; init; }
    public required string DestinationSystemName { get; init; }
    public string? DestinationStationType { get; init; }
    public required long CommodityId { get; init; }
    public required string CommodityName { get; init; }
    public required long BuyPrice { get; init; }
    public required long SellPrice { get; init; }
    public required int Quantity { get; init; }
    public required long TotalProfit { get; init; }
}
