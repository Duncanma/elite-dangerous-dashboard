namespace EliteDashboard.Data.Entities;

public sealed class ColonisationSystemClaim
{
    public long Id { get; set; }
    public required string StarSystem { get; set; }
    public long SystemAddress { get; set; }
    public DateTime ClaimedUtc { get; set; }
    public bool Released { get; set; }
}
