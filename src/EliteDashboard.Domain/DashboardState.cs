namespace EliteDashboard.Domain;

/// <summary>
/// The single reconciled "current state" aggregate the whole dashboard reads
/// from. Rebuilt by replaying the latest journal file's startup block plus
/// the snapshot files every time the app starts - never persisted directly
/// (the raw event history in JournalEventRaw is what's durable; this is a
/// derived, in-memory view).
/// </summary>
public sealed record DashboardState
{
    public string? FID { get; init; }
    public string? Name { get; init; }
    public string? CurrentSystem { get; init; }
    public string? CurrentStation { get; init; }
    public bool Docked { get; init; }

    public string? ShipType { get; init; }
    public string? ShipTypeDisplay { get; init; }
    public string? ShipName { get; init; }
    public string? ShipIdent { get; init; }
    public int? ShipId { get; init; }
    public double CargoCapacity { get; init; }
    public double MaxJumpRange { get; init; }
    public long Rebuy { get; init; }
    public long HullValue { get; init; }
    public long ModulesValue { get; init; }

    public RankStanding Rank { get; init; } = RankStanding.Empty;
    public EconomicSnapshot Economy { get; init; } = EconomicSnapshot.Empty;
    public ShipStatusSnapshot ShipStatus { get; init; } = ShipStatusSnapshot.Empty;
    public IReadOnlyList<ShipSummary> Fleet { get; init; } = [];

    /// <summary>
    /// Full module loadout for the ship currently being flown only - the
    /// journal doesn't expose per-module detail for stored ships, just type/location/value.
    /// </summary>
    public IReadOnlyList<ShipModuleSummary> CurrentShipModules { get; init; } = [];

    public DateTimeOffset LastUpdatedUtc { get; init; }

    public static readonly DashboardState Empty = new();
}
