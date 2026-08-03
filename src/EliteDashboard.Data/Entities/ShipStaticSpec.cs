namespace EliteDashboard.Data.Entities;

/// <summary>
/// Reference stats joined from two real, independently-verifiable sources:
/// EDCD/FDevIDs' shipyard.csv maps Frontier's internal ship symbol (e.g.
/// "DiamondBackXL") to its real display name ("Diamondback Explorer"), and
/// EDCD/coriolis-data's ship files are keyed by that same display name. This
/// is a derived join, not a guessed mapping - ships not yet in coriolis-data
/// (e.g. very recently added ones) simply have no matching row here.
/// </summary>
public sealed class ShipStaticSpec
{
    public long Id { get; set; }

    /// <summary>Frontier's internal symbol, matches DashboardState/ShipSummary.ShipType.</summary>
    public required string Symbol { get; set; }

    public required string DisplayName { get; set; }
    public string? Manufacturer { get; set; }

    /// <summary>Coriolis "class": 1=Small, 2=Medium, 3=Large.</summary>
    public int SizeClass { get; set; }

    public double HullMass { get; set; }
    public long HullCost { get; set; }
    public int BaseShieldStrength { get; set; }
    public int BaseArmour { get; set; }
    public int CrewSeats { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
