namespace EliteDashboard.Data.Entities;

/// <summary>
/// References a ship by its journal ShipID/ShipType rather than a DB foreign
/// key - fleet composition lives in the in-memory StateStore (rebuilt from
/// journal replay), not a persisted Ship table.
/// </summary>
public sealed class ShipUpgradePlan
{
    public long Id { get; set; }
    public required int ShipId { get; set; }
    public string? ShipType { get; set; }
    public required string ModuleSlot { get; set; }
    public string? CurrentModuleSymbol { get; set; }
    public string? DesiredModuleSymbol { get; set; }
    public string? DesiredEngineeringNotes { get; set; }
    public long? EstimatedCost { get; set; }
    public int Priority { get; set; }
    public required DateTime CreatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
