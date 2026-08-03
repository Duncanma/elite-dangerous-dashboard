namespace EliteDashboard.Data.Entities;

/// <summary>
/// Keyed by MarketId (Frontier's own stable station identifier) - the
/// canonical join key used everywhere, including resolving Colonisation
/// sites (which carry only a MarketID, no system/station name) in Phase 6.
/// Upserted both from EDDN and from the commander's own Docked events.
/// </summary>
public sealed class Station
{
    public long Id { get; set; }
    public required long MarketId { get; set; }
    public required string Name { get; set; }
    public long StarSystemId { get; set; }
    public StarSystem? StarSystem { get; set; }
    public string? StationType { get; set; }

    /// <summary>
    /// Real per-station pad counts from our own Docked events - only known
    /// for stations we've personally visited. Null (not 0) means unknown,
    /// same "can't distinguish unset from a real zero" reasoning as
    /// StarSystem.HasCoordinates - a station could plausibly have 0 of a
    /// given pad size for real, so these must stay nullable.
    /// </summary>
    public int? SmallPads { get; set; }
    public int? MediumPads { get; set; }
    public int? LargePads { get; set; }

    public DateTime LastUpdatedUtc { get; set; }
    public string LastUpdateSource { get; set; } = "Unknown";
}
