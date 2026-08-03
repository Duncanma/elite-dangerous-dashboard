namespace EliteDashboard.Data.Entities;

public sealed class StarSystem
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    /// <summary>
    /// X/Y/Z default to 0 - indistinguishable from "unknown" vs "genuinely at
    /// Sol's real (0,0,0) position" without this flag. Set once real
    /// coordinates are known, either from the commander's own FSDJump events
    /// or an EDSM lookup for systems only known via EDDN's price feed.
    /// </summary>
    public bool HasCoordinates { get; set; }

    public DateTime LastUpdatedUtc { get; set; }

    public List<Station> Stations { get; set; } = [];
}
