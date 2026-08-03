namespace EliteDashboard.Data.Entities;

public sealed class CreditGoal
{
    public long Id { get; set; }
    public required long TargetAmount { get; set; }

    /// <summary>DateTime, not DateTimeOffset - see JournalEventRaw.TimestampUtc for why.</summary>
    public DateTime? TargetDate { get; set; }

    public required DateTime CreatedUtc { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
}
