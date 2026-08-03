namespace EliteDashboard.Domain;

public sealed record RankStanding
{
    public int Combat { get; init; }
    public int Trade { get; init; }
    public int Explore { get; init; }
    public int CQC { get; init; }
    public int Federation { get; init; }
    public int Empire { get; init; }

    /// <summary>Percent progress (0-99) toward the next tier, from the journal's Progress event.</summary>
    public int CombatProgress { get; init; }
    public int TradeProgress { get; init; }
    public int ExploreProgress { get; init; }
    public int CqcProgress { get; init; }
    public int FederationProgress { get; init; }
    public int EmpireProgress { get; init; }

    public static readonly RankStanding Empty = new();
}
