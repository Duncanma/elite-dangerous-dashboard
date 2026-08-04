namespace EliteDashboard.Data.Entities;

/// <summary>
/// Durable per-ship snapshot of the last Loadout event seen for that ship:
/// full module list plus the ship-level figures the event carries (cargo
/// capacity, jump range, fuel tank, values, rebuy) and when it was seen.
///
/// The journal only exposes per-module detail for the ship the commander is
/// currently sitting in, so a stored ship's row is only refreshed when the
/// player boards it. Written by JournalStateReconciler on every Loadout
/// event (upsert, newest timestamp wins); rows for ships that leave the
/// fleet (sold) are pruned by the same fleet resync that maintains Ship.
/// </summary>
public sealed class ShipLoadout
{
    public long Id { get; set; }
    public required int ShipId { get; set; }
    public string? ShipType { get; set; }

    /// <summary>Timestamp of the Loadout event this snapshot came from.</summary>
    public DateTime SeenAtUtc { get; set; }

    public double CargoCapacity { get; set; }
    public double MaxJumpRange { get; set; }
    public double FuelCapacityMain { get; set; }
    public double UnladenMass { get; set; }
    public long Rebuy { get; set; }
    public long HullValue { get; set; }
    public long ModulesValue { get; set; }

    /// <summary>
    /// JSON array of Domain.ShipModuleSummary. A plain text column, not a
    /// child table - nothing ever queries into individual modules, the
    /// loadout is only ever read whole (by the fleet loadout endpoint).
    /// </summary>
    public required string ModulesJson { get; set; }
}
