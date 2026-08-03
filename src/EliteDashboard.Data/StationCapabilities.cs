using EliteDashboard.Data.Entities;

namespace EliteDashboard.Data;

/// <summary>
/// Infers a station's landing-pad and planetary/mobile characteristics from
/// whatever we know about it: real per-station pad counts when we've docked
/// there ourselves, otherwise a conservative reading of its Frontier-internal
/// StationType (captured either from our own Docked events or from EDDN's
/// commodity messages, which carry the same vocabulary).
/// </summary>
public static class StationCapabilities
{
    // Confirmed against the commander's own real per-station LandingPads
    // history: every dock at one of these types had at least one Large pad.
    private static readonly HashSet<string> ReliableLargePadTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coriolis", "Orbis", "Ocellus", "Dodec", "AsteroidBase", "FleetCarrier",
    };

    // Confirmed against real data: every real Outpost dock had zero Large pads.
    private static readonly HashSet<string> ReliableNoLargePadTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Outpost",
    };

    // Planetary/surface installation types - relevant for the "no planets"
    // trade route filter. NOT used for pad-size inference: CraterOutpost and
    // SurfaceStation were confirmed (via the commander's own real dock
    // history) to have wildly inconsistent Large-pad counts (0 to 16 for the
    // same type), so pad size for these must come from real per-station data
    // or stay unknown.
    private static readonly HashSet<string> PlanetaryStationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SurfaceStation", "CraterPort", "CraterOutpost", "OnFootSettlement",
    };

    /// <summary>
    /// True/false when real pad counts are known or the station's type
    /// reliably implies an answer either way; null when we genuinely don't know.
    /// </summary>
    public static bool? SupportsLargePads(Station station)
    {
        if (station.LargePads.HasValue)
        {
            return station.LargePads.Value > 0;
        }

        if (station.StationType is null)
        {
            return null;
        }

        if (ReliableLargePadTypes.Contains(station.StationType))
        {
            return true;
        }

        if (ReliableNoLargePadTypes.Contains(station.StationType))
        {
            return false;
        }

        return null;
    }

    /// <summary>
    /// Whether a ship of the given Coriolis size class (1=Small, 2=Medium,
    /// 3=Large - matches <see cref="ShipStaticSpec.SizeClass"/>) can dock
    /// here. True/false when real pad counts or a reliable type give an
    /// answer, null when genuinely unknown. Small and Medium are treated as
    /// always-supported absent real evidence otherwise - every station type
    /// observed in the commander's own dock history had at least one pad of
    /// each; only Large-pad availability is unreliable enough to need the
    /// careful handling in <see cref="SupportsLargePads"/>.
    /// </summary>
    public static bool? SupportsShipSize(Station station, int shipSizeClass)
    {
        if (shipSizeClass <= 1)
        {
            return true;
        }

        if (station.LargePads.HasValue)
        {
            return shipSizeClass == 2
                ? (station.MediumPads ?? 0) > 0 || station.LargePads.Value > 0
                : station.LargePads.Value > 0;
        }

        return shipSizeClass == 2 ? true : SupportsLargePads(station);
    }

    public static bool IsPlanetary(Station station) =>
        station.StationType is not null && PlanetaryStationTypes.Contains(station.StationType);

    public static bool IsFleetCarrier(Station station) =>
        string.Equals(station.StationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);
}
