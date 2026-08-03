namespace EliteDashboard.Data.Entities;

public sealed class ShipWishlistItem
{
    public long Id { get; set; }
    public required string ShipType { get; set; }
    public string? DisplayName { get; set; }
    public string? TargetLoadoutNotes { get; set; }
    public long? EstimatedCost { get; set; }
    public int Priority { get; set; }
    public required DateTime CreatedUtc { get; set; }

    /// <summary>Marked once bought, rather than deleted - keeps a little history.</summary>
    public DateTime? PurchasedUtc { get; set; }
}
