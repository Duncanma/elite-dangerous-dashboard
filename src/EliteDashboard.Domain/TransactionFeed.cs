namespace EliteDashboard.Domain;

public sealed record TransactionRecord
{
    public required long Id { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required TransactionCategory Category { get; init; }
    public required long Amount { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Live push channel for newly-recorded transactions, separate from
/// StateStore since this is a stream of discrete events (append to a list)
/// rather than a single "current value" to replace.
/// </summary>
public sealed class TransactionFeed
{
    public event Action<TransactionRecord>? Added;

    public void Publish(TransactionRecord record) => Added?.Invoke(record);
}
