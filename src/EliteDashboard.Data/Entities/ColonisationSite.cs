namespace EliteDashboard.Data.Entities;

/// <summary>
/// Keyed by MarketId, same as Station - the ColonisationConstructionDepot
/// event itself carries only a MarketID, no system/station name, so location
/// is resolved by joining against Station (populated by the commander's own
/// Docked events at the site).
/// </summary>
public sealed class ColonisationSite
{
    public long Id { get; set; }
    public required long MarketId { get; set; }
    public double ConstructionProgress { get; set; }
    public bool ConstructionComplete { get; set; }
    public bool ConstructionFailed { get; set; }

    /// <summary>
    /// True once a ColonisationContribution event (actual cargo delivered)
    /// has been seen for this site. ColonisationConstructionDepot alone
    /// (which creates this row) fires for any site with the contribution
    /// service that the commander has merely docked at, so without this flag
    /// "My Systems" would show every construction site ever passed through,
    /// not just ones actually being built by the commander.
    /// </summary>
    public bool HasContributed { get; set; }

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }

    public List<ColonisationResourceRequirement> Resources { get; set; } = [];
}
