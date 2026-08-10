using EliteDashboard.Journal;

namespace EliteDashboard.Tests;

/// <summary>
/// Dedup keys are (JournalFile, LineNumber). LineNumber must mean the same
/// thing in the live tailer and the historical backfill, or a whitespace-only
/// line shifts every subsequent key (duplicates or silent drops). See #6.
/// </summary>
public sealed class JournalLineNumberingTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("\r", true)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    [InlineData("{}", false)]
    public void IsBlankPhysicalLine_MatchesTailerSemantics(string line, bool expectedBlank)
    {
        Assert.Equal(expectedBlank, JournalLineNumbering.IsBlankPhysicalLine(line));
    }

    [Fact]
    public void NumberNonBlankLines_CountsWhitespaceOnly_SkipsEmpty()
    {
        // Empty line is skipped; a whitespace-only line is counted (same as
        // the live tailer historically did). That keeps existing dedup keys
        // stable for rows written by the tailer.
        var numbered = JournalLineNumbering.NumberNonBlankLines(
            ["{\"event\":\"A\"}", "", "   ", "{\"event\":\"B\"}"]).ToList();

        Assert.Equal(3, numbered.Count);
        Assert.Equal(1, numbered[0].LineNumber);
        Assert.Equal("{\"event\":\"A\"}", numbered[0].Content);
        Assert.Equal(2, numbered[1].LineNumber);
        Assert.Equal("   ", numbered[1].Content);
        Assert.Equal(3, numbered[2].LineNumber);
        Assert.Equal("{\"event\":\"B\"}", numbered[2].Content);
    }

    [Fact]
    public void NormalizeEmittedLine_StripsBomAndTrailingCr()
    {
        Assert.Equal("{}", JournalLineNumbering.NormalizeEmittedLine("\uFEFF{}\r"));
        Assert.Equal("   ", JournalLineNumbering.NormalizeEmittedLine("   \r"));
    }
}
