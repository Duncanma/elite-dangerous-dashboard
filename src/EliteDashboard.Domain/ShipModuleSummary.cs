namespace EliteDashboard.Domain;

public sealed record ShipModuleSummary
{
    public string? Slot { get; init; }
    public string? Item { get; init; }
    public bool On { get; init; }
    public int Priority { get; init; }
    public double Health { get; init; }
    public string? EngineeringBlueprint { get; init; }
    public int EngineeringLevel { get; init; }
    public string? ExperimentalEffect { get; init; }
}
