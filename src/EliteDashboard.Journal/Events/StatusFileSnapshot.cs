namespace EliteDashboard.Journal.Events;

public sealed record FuelInfo
{
    public double FuelMain { get; init; }
    public double FuelReservoir { get; init; }
}

/// <summary>
/// Status.json: the live flight-HUD snapshot, overwritten roughly once a
/// second while playing. Only the fields the dashboard currently needs are
/// modeled - System.Text.Json ignores the rest.
/// </summary>
public sealed record StatusFileSnapshot
{
    public long Flags { get; init; }
    public double Cargo { get; init; }
    public FuelInfo? Fuel { get; init; }
    public string? LegalState { get; init; }
}
