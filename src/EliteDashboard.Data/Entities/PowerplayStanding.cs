namespace EliteDashboard.Data.Entities;

/// <summary>Effectively a singleton - one row holding the commander's current pledge.</summary>
public sealed class PowerplayStanding
{
    public long Id { get; set; }
    public required string Power { get; set; }
    public int Rank { get; set; }
    public long TotalMerits { get; set; }
    public long TimePledgedSeconds { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
