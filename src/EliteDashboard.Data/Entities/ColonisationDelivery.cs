namespace EliteDashboard.Data.Entities;

/// <summary>
/// One commodity line from a ColonisationContribution event — cargo actually
/// delivered to a construction site. Zero-credit by design (not a Transaction).
/// Dedup key is (JournalFile, LineNumber, CommodityName) because one journal
/// line can carry several commodities.
/// </summary>
public sealed class ColonisationDelivery
{
    public long Id { get; set; }
    public required long MarketId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public required string CommodityName { get; set; }
    public string? CommodityDisplayName { get; set; }
    public long Amount { get; set; }
    public required string JournalFile { get; set; }
    public long LineNumber { get; set; }
}
