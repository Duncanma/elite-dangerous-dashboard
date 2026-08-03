using EliteDashboard.Domain;

namespace EliteDashboard.Data.Entities;

/// <summary>
/// One signed row per credit-affecting event. Deliberately not paired into
/// synthetic "trip P&amp;L" records - "trading profit" on the Economics view
/// is a filtered sum over TradeBuy+TradeSell computed at query time, which
/// matches how the game itself reports things.
/// </summary>
public sealed class Transaction
{
    public long Id { get; set; }

    /// <summary>
    /// Stored as DateTime (not DateTimeOffset) because the SQLite EF Core
    /// provider can't translate ORDER BY on DateTimeOffset columns. Always UTC.
    /// </summary>
    public required DateTime TimestampUtc { get; set; }

    public required TransactionCategory Category { get; set; }

    /// <summary>Signed: negative for spend, positive for income.</summary>
    public required long Amount { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Source journal line, used to detect a re-processed line (the tailer
    /// always replays the current journal file from the start on every app
    /// restart) so the same real-world transaction isn't recorded twice.
    /// Null for rows written before this dedup key existed.
    /// </summary>
    public string? JournalFile { get; set; }

    public long? LineNumber { get; set; }
}
