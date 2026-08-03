namespace EliteDashboard.Data.Entities;

/// <summary>Append-only time series backing the merits-over-time chart.</summary>
public sealed class PowerplayStandingHistory
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public required string Power { get; set; }
    public long TotalMerits { get; set; }
    public long MeritsDelta { get; set; }

    /// <summary>
    /// Source journal line, used to detect a re-processed line (the tailer
    /// always replays the current journal file from the start on every app
    /// restart) so the same standing update isn't logged twice.
    /// Null for rows written before this dedup key existed.
    /// </summary>
    public string? JournalFile { get; set; }

    public long? LineNumber { get; set; }
}
