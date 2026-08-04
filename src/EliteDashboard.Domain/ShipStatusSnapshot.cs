namespace EliteDashboard.Domain;

/// <summary>
/// Live flight-HUD state, sourced from Status.json (overwritten in place by
/// the game roughly once a second, not appended like the journal).
/// </summary>
public sealed record ShipStatusSnapshot
{
    public long Flags { get; init; }
    public double CargoTons { get; init; }
    public double FuelMain { get; init; }
    public string? LegalState { get; init; }

    public static readonly ShipStatusSnapshot Empty = new();
}
