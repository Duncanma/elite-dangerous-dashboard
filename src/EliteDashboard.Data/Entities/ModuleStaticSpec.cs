namespace EliteDashboard.Data.Entities;

/// <summary>
/// Reference data from EDCD/FDevIDs' outfitting.csv, which maps Frontier's
/// internal module symbol (e.g. "Hpt_MultiCannon_Gimbal_Medium") directly to
/// its real display name ("Multi-Cannon") plus mount/class/rating - no join
/// against a second source needed, unlike ShipStaticSpec.
/// </summary>
public sealed class ModuleStaticSpec
{
    public long Id { get; set; }

    /// <summary>Frontier's internal symbol, matches ShipModuleSummary.Item (case-insensitively - see FleetEndpoints).</summary>
    public required string Symbol { get; set; }

    public required string DisplayName { get; set; }
    public string? Category { get; set; }
    public string? Mount { get; set; }
    public string? Guidance { get; set; }
    public int? Class { get; set; }
    public string? Rating { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
