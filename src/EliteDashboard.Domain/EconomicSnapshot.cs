namespace EliteDashboard.Domain;

/// <summary>
/// Current-balance fields plus Frontier's own lifetime counters from the
/// Statistics event. The lifetime counters are kept verbatim (not derived)
/// so later phases can cross-check the transaction ledger against them.
/// </summary>
public sealed record EconomicSnapshot
{
    public long Credits { get; init; }
    public long Loan { get; init; }
    public long CurrentWealth { get; init; }
    public long SpentOnShips { get; init; }
    public long SpentOnOutfitting { get; init; }
    public long SpentOnRepairs { get; init; }
    public long SpentOnFuel { get; init; }
    public long SpentOnAmmoConsumables { get; init; }
    public long InsuranceClaims { get; init; }

    public static readonly EconomicSnapshot Empty = new();
}
