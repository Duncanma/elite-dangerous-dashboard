namespace EliteDashboard.Data.Entities;

public sealed class ColonisationResourceRequirement
{
    public long Id { get; set; }
    public required long ColonisationSiteId { get; set; }
    public ColonisationSite? Site { get; set; }

    public required string CommodityName { get; set; }
    public string? CommodityDisplayName { get; set; }
    public long RequiredAmount { get; set; }
    public long ProvidedAmount { get; set; }
    public long Payment { get; set; }
}
