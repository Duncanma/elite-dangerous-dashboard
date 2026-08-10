namespace EliteDashboard.Journal;

/// <summary>
/// Shared blank-line / numbering rules for the live <see cref="JournalTailer"/>
/// and historical <see cref="JournalBackfillService"/>. Transaction (and other)
/// dedup keys are <c>(JournalFile, LineNumber)</c>, so both paths must count
/// the same physical lines the same way — otherwise a whitespace-only line
/// shifts every subsequent key (duplicates or silent drops). See issue #6.
/// </summary>
public static class JournalLineNumbering
{
    /// <summary>
    /// True when the line should not be counted or emitted. Empty lines (after
    /// stripping a trailing CR) are blank; whitespace-only lines are not —
    /// matching the historical live-tailer rule so existing dedup keys stay valid.
    /// </summary>
    public static bool IsBlankPhysicalLine(string line)
        => line.TrimEnd('\r').Length == 0;

    /// <summary>
    /// Content form emitted to the reconciler: strip trailing CR and a leading BOM.
    /// </summary>
    public static string NormalizeEmittedLine(string line)
    {
        var normalized = line.TrimEnd('\r');
        if (normalized.Length > 0 && normalized[0] == '\uFEFF')
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    /// <summary>
    /// Assigns 1-based line numbers to non-blank physical lines, in order.
    /// Useful for tests and any whole-file reader that must match the tailer.
    /// </summary>
    public static IEnumerable<(long LineNumber, string Content)> NumberNonBlankLines(IEnumerable<string> physicalLines)
    {
        long number = 0;
        foreach (var line in physicalLines)
        {
            if (IsBlankPhysicalLine(line))
            {
                continue;
            }

            number++;
            yield return (number, NormalizeEmittedLine(line));
        }
    }
}
