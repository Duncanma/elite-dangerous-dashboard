using System.Globalization;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Journal;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

public sealed class JournalBackfillServiceTests : IDisposable
{
    private const string ProcessedThroughKey = "Journal.BackfillProcessedThroughUtc";
    private const string LegacyCompleteKey = "Journal.BackfillCompleteUtc";

    private static readonly DateTime OldFileTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NewFileTime = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LatestFileTime = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _dir;

    public JournalBackfillServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "edd-backfill-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public async Task ProcessesOnlyFilesNewerThanTheHighWaterMark()
    {
        using var db = new TestDb();
        var oldFile = WriteJournal("Journal.2026-01-01T000000.01.log", OldFileTime);
        var newFile = WriteJournal("Journal.2026-01-02T000000.01.log", NewFileTime);
        var latest = WriteJournal("Journal.2026-01-03T000000.01.log", LatestFileTime);
        await SeedSetting(db, ProcessedThroughKey, "2026-01-01T12:00:00.0000000+00:00");

        await RunBackfillAsync(db);

        await using var check = db.CreateContext();
        var archivedFiles = await ArchivedFilesAsync(check);
        Assert.DoesNotContain(archivedFiles, f => f == oldFile);
        Assert.Contains(archivedFiles, f => f == newFile);
        Assert.DoesNotContain(archivedFiles, f => f == latest);

        var mark = await check.AppSettings.FindAsync(ProcessedThroughKey);
        Assert.NotNull(mark);
        Assert.Equal(
            new DateTimeOffset(NewFileTime, TimeSpan.Zero),
            DateTimeOffset.Parse(mark!.Value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public async Task SeedsHighWaterMarkFromTheLegacyOnceEverFlag()
    {
        // Installs that already ran the old once-ever backfill carry only
        // Journal.BackfillCompleteUtc. Seeding the mark from it picks up
        // exactly the journal files created since - the silent history gap.
        using var db = new TestDb();
        var oldFile = WriteJournal("Journal.2026-01-01T000000.01.log", OldFileTime);
        var newFile = WriteJournal("Journal.2026-01-02T000000.01.log", NewFileTime);
        var latest = WriteJournal("Journal.2026-01-03T000000.01.log", LatestFileTime);
        await SeedSetting(db, LegacyCompleteKey, "2026-01-01T12:00:00.0000000+00:00");

        await RunBackfillAsync(db);

        await using var check = db.CreateContext();
        var archivedFiles = await ArchivedFilesAsync(check);
        Assert.DoesNotContain(archivedFiles, f => f == oldFile);
        Assert.Contains(archivedFiles, f => f == newFile);
        Assert.DoesNotContain(archivedFiles, f => f == latest);

        var mark = await check.AppSettings.FindAsync(ProcessedThroughKey);
        Assert.NotNull(mark);
        Assert.Equal(
            new DateTimeOffset(NewFileTime, TimeSpan.Zero),
            DateTimeOffset.Parse(mark!.Value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public async Task WithNoMark_ProcessesEverythingExceptTheLatestFile()
    {
        using var db = new TestDb();
        var oldFile = WriteJournal("Journal.2026-01-01T000000.01.log", OldFileTime);
        var newFile = WriteJournal("Journal.2026-01-02T000000.01.log", NewFileTime);
        var latest = WriteJournal("Journal.2026-01-03T000000.01.log", LatestFileTime);

        await RunBackfillAsync(db);

        await using var check = db.CreateContext();
        var archivedFiles = await ArchivedFilesAsync(check);
        Assert.Contains(archivedFiles, f => f == oldFile);
        Assert.Contains(archivedFiles, f => f == newFile);
        Assert.DoesNotContain(archivedFiles, f => f == latest);

        Assert.NotNull(await check.AppSettings.FindAsync(ProcessedThroughKey));
    }

    private string WriteJournal(string name, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllLines(path,
        [
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Fileheader","part":1}""",
            """{"timestamp":"2026-01-01T00:00:02Z","event":"Fileheader","part":2}""",
        ]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    private static async Task SeedSetting(TestDb db, string key, string value)
    {
        await using var ctx = db.CreateContext();
        ctx.AppSettings.Add(new AppSetting { Key = key, Value = value });
        await ctx.SaveChangesAsync();
    }

    private async Task RunBackfillAsync(TestDb db)
    {
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = new JournalStateReconciler(
            store,
            queue,
            new TransactionFeed(),
            new TradeRoutePlanService(db.Factory),
            db.Factory,
            NullLogger<JournalStateReconciler>.Instance);
        var service = new JournalBackfillService(
            new JournalOptions { JournalPathOverride = _dir },
            reconciler,
            queue,
            store,
            db.Factory,
            NullLogger<JournalBackfillService>.Instance);

        // The reconciler's writes are fire-and-forget; run the real writer so
        // the queue drains and the final mark write (which is awaited) lands.
        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            await service.BackfillIfNeededAsync();
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<List<string>> ArchivedFilesAsync(DashboardDbContext db)
        => await db.JournalEvents.Select(e => e.JournalFile).Distinct().ToListAsync();

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
