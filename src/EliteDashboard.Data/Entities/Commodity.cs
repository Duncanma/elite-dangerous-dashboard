namespace EliteDashboard.Data.Entities;

public sealed class Commodity
{
    public long Id { get; set; }

    /// <summary>Frontier's internal symbol, e.g. "platinum" - stable across locales.</summary>
    public required string InternalSymbol { get; set; }

    public required string DisplayName { get; set; }
}
