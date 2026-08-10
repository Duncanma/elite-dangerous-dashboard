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
    public void Tick_WhitespaceOnlyLine_IsNumberedLikeEmptyIsNot()
    {
        // Regression for #6: a whitespace-only line must receive a LineNumber
        // (the backfill used to skip it with IsNullOrWhiteSpace, desyncing keys).
        var path = Path.Combine(_dir, "Journal.2026-01-01T000000.02.log");
        File.WriteAllText(path, "line one\n   \nline three\n");

        using var tailer = new JournalTailer(
            new JournalOptions { JournalPathOverride = _dir },
            NullLogger<JournalTailer>.Instance);
        var received = new List<(long Number, string Json)>();
        tailer.LineRead += line => received.Add((line.LineNumber, line.Json));

        tailer.Tick();

        Assert.Equal(3, received.Count);
        Assert.Equal((1L, "line one"), received[0]);
        Assert.Equal((2L, "   "), received[1]);
        Assert.Equal((3L, "line three"), received[2]);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
