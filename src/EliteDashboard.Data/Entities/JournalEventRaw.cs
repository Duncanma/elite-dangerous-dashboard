namespace EliteDashboard.Data.Entities;

/// <summary>
/// One row per journal event line, stored verbatim. This is the audit log /
/// schema-drift safety net: even event types the app doesn't model yet are
/// captured here, and later phases (Economics transactions, Powerplay
/// history, etc.) derive their tables from this raw history.
/// </summary>
public sealed class JournalEventRaw
{
    public long Id { get; set; }

    /// <summary>
    /// Stored as DateTime (not DateTimeOffset) because the SQLite EF Core
    /// provider can't translate ORDER BY on DateTimeOffset columns. Always UTC.
    /// </summary>
    public required DateTime TimestampUtc { get; set; }
    public required string EventType { get; set; }
    public required string RawJson { get; set; }
    public required string JournalFile { get; set; }
    public long LineNumber { get; set; }
}
