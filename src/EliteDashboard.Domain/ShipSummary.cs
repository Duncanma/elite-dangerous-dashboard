namespace EliteDashboard.Domain;

public enum ShipLocationKind
{
    /// <summary>The ship currently being flown.</summary>
    Current,

    /// <summary>Stored at the shipyard the commander is currently at.</summary>
    Here,

    /// <summary>Stored at a different station/system.</summary>
    Remote,

    /// <summary>Mid-transfer between stations.</summary>
    InTransit,
}

public sealed record ShipSummary
{
    public required int ShipId { get; init; }
    public string? ShipType { get; init; }
    public string? ShipTypeDisplay { get; init; }
    public string? Name { get; init; }
    public string? StarSystem { get; init; }
    public ShipLocationKind Location { get; init; }
}
