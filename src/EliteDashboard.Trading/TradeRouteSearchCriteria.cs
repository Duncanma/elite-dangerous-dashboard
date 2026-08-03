namespace EliteDashboard.Trading;

/// <summary>
/// Everything that constrains a route search. When <see cref="OriginStationId"/>
/// is set the search is anchored to that exact station (used when chaining
/// multi-hop legs, or when the caller names an explicit start); otherwise
/// candidate origins are limited to <see cref="MaxJumpsFromOrigin"/> jumps of
/// <see cref="OriginSystemName"/>, using <see cref="ShipJumpRangeLy"/> as the
/// per-jump distance. Distances are straight-line ("as the crow flies")
/// between known/resolved system coordinates, not actual plotted jump
/// routes - a deliberate approximation, same tradeoff already accepted for
/// the colonisation shipping planner.
/// </summary>
public sealed record TradeRouteSearchCriteria
{
    public long? OriginStationId { get; init; }
    public string? OriginSystemName { get; init; }
    public required int CargoCapacity { get; init; }
    public required long Credits { get; init; }
    public int MaxHops { get; init; } = 3;

    public double ShipJumpRangeLy { get; init; }
    public int MaxJumpsFromOrigin { get; init; } = 5;
    public int MaxJumpsBetweenStations { get; init; } = 2;

    /// <summary>1=Small, 2=Medium, 3=Large (matches ShipStaticSpec.SizeClass); null = no pad-size constraint.</summary>
    public int? RequiredShipSizeClass { get; init; }

    /// <summary>Whether to include stations whose pad size we can't determine at all, rather than excluding them by default.</summary>
    public bool IncludeUnknownPadSize { get; init; }

    public bool ExcludePlanetary { get; init; } = true;
    public bool ExcludeFleetCarriers { get; init; } = true;
}
