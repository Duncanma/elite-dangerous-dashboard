using EliteDashboard.Journal;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

public sealed class JournalTailerTests : IDisposable
{
    private readonly string _dir;

    public JournalTailerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "edd-tailer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public void Tick_AHandlerThrows_LineIsSkippedAndLaterLinesStillFlow()
    {
        // The tailer drives the parser + reconciler synchronously from the
        // poll timer - an exception escaping here kills the whole host.
        var path = Path.Combine(_dir, "Journal.2026-01-01T000000.01.log");
        File.WriteAllLines(path, ["line one", "line two", "line three"]);

        using var tailer = new JournalTailer(
            new JournalOptions { JournalPathOverride = _dir },
            NullLogger<JournalTailer>.Instance);
        var received = new List<long>();
        tailer.LineRead += line =>
        {
            if (line.LineNumber == 2)
            {
                throw new InvalidOperationException("boom");
            }
            received.Add(line.LineNumber);
        };

        var ex = Record.Exception(tailer.Tick);

        Assert.Null(ex);
        Assert.Equal([1L, 3L], received);
    }

    [Fact]
    public void Tick_FileRotates_TrailingLinesOfOutgoingFileAreStillRead()
    {
        // The game keeps appending to a part file right up to the moment it
        // rotates, so the last events of a part exist only in the outgoing
        // file. The tailer never revisits it after switching, so anything not
        // drained on the way out is lost from the live path entirely.
        var first = Path.Combine(_dir, "Journal.2026-01-01T000000.01.log");
        File.WriteAllLines(first, ["one", "two"]);
        File.SetLastWriteTimeUtc(first, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        using var tailer = new JournalTailer(
            new JournalOptions { JournalPathOverride = _dir },
            NullLogger<JournalTailer>.Instance);
        var received = new List<string>();
        tailer.LineRead += line => received.Add(line.Json);

        tailer.Tick();
        Assert.Equal(["one", "two"], received);

        // Written after the tick above, then the game rotates to a new part.
        File.AppendAllLines(first, ["three", "four"]);
        File.SetLastWriteTimeUtc(first, new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc));

        var second = Path.Combine(_dir, "Journal.2026-01-01T010000.01.log");
        File.WriteAllLines(second, ["five"]);
        File.SetLastWriteTimeUtc(second, new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));

        tailer.Tick();

        Assert.Equal(["one", "two", "three", "four", "five"], received);
    }

    [Fact]
    public void Tick_FileRotates_LineNumbersRestartForTheNewFile()
    {
        // Transaction dedup is keyed on (JournalFile, LineNumber), so numbering
        // has to restart per file - and the drain-before-switch must not leak
        // the outgoing file's counter into the new one.
        var first = Path.Combine(_dir, "Journal.2026-01-01T000000.01.log");
        File.WriteAllLines(first, ["one", "two", "three"]);
        File.SetLastWriteTimeUtc(first, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        using var tailer = new JournalTailer(
            new JournalOptions { JournalPathOverride = _dir },
            NullLogger<JournalTailer>.Instance);
        var received = new List<JournalLine>();
        tailer.LineRead += received.Add;

        tailer.Tick();

        var second = Path.Combine(_dir, "Journal.2026-01-01T010000.01.log");
        File.WriteAllLines(second, ["four"]);
        File.SetLastWriteTimeUtc(second, new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));

        tailer.Tick();

        var last = received[^1];
        Assert.Equal(second, last.FilePath);
        Assert.Equal(1, last.LineNumber);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
