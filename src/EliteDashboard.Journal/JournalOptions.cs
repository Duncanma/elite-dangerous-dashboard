namespace EliteDashboard.Journal;

public sealed class JournalOptions
{
    public const string SectionName = "Journal";

    /// <summary>
    /// Overrides the journal directory. Leave null/empty to use the real
    /// Elite Dangerous Saved Games folder. Used by tests/replay tooling to
    /// point at a scratch fixture directory instead.
    /// </summary>
    public string? JournalPathOverride { get; set; }

    /// <summary>
    /// Fallback poll interval. FileSystemWatcher is used for low-latency
    /// notification, but it can silently miss events under load, so this
    /// timer re-checks the current file (and for a newer file) regardless.
    /// </summary>
    public int PollIntervalMs { get; set; } = 1000;
}
