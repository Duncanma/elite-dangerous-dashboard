using System.Globalization;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.Journal;

/// <summary>
/// Replays completed journal files through the same reconciler path the live
/// tailer uses, so historical sessions rebuild into state and archive. Progress
/// is tracked as a high-water mark (the newest file-write time processed), so
/// every startup also picks up sessions that ran while the app was offline.
/// Reprocessing is safe: every persisted row is guarded by a
/// (JournalFile, LineNumber) or replay-dedup existence check, so a second pass
/// is a stream of cheap no-ops. The current (latest) file is always excluded -
/// the live tailer owns it.
/// </summary>
public sealed class JournalBackfillService
{
    private const string ProcessedThroughSettingKey = "Journal.BackfillProcessedThroughUtc";

    // The original once-ever flag. Kept only as a migration source: installs
    // that already ran it get their high-water mark seeded from its timestamp
    // on the first run of this version, which picks up exactly the sessions
    // the old backfill missed.
    private const string LegacyCompleteSettingKey = "Journal.BackfillCompleteUtc";

    private readonly JournalOptions _options;
    private readonly JournalStateReconciler _reconciler;
    private readonly DbWriteQueue _writeQueue;
    private readonly StateStore _store;
    private readonly IDbContextFactory<DashboardDbContext> _dbContextFactory;
    private readonly ILogger<JournalBackfillService> _logger;

    public JournalBackfillService(
        JournalOptions options, JournalStateReconciler reconciler, DbWriteQueue writeQueue,
        StateStore store, IDbContextFactory<DashboardDbContext> dbContextFactory,
        ILogger<JournalBackfillService> logger)
    {
        _options = options;
        _reconciler = reconciler;
        _writeQueue = writeQueue;
        _store = store;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task BackfillIfNeededAsync(CancellationToken ct = default)
    {
        var processedThrough = await LoadHighWaterMarkAsync(ct);

        var directory = JournalFileSetLocator.ResolveJournalDirectory(_options.JournalPathOverride);
        if (!Directory.Exists(directory))
        {
            _logger.LogInformation("Journal directory {Directory} does not exist yet; skipping backfill", directory);
            return;
        }

        // The tailer owns the current (latest) file; only completed files are
        // backfilled, and only those written after the high-water mark.
        var latest = JournalFileSetLocator.FindLatestJournalFile(directory);
        var filesToProcess = new DirectoryInfo(directory)
            .GetFiles("Journal.*.log")
            .Where(f => latest is null || !string.Equals(f.FullName, latest.FullName, StringComparison.OrdinalIgnoreCase))
            .Where(f => processedThrough is null || f.LastWriteTimeUtc > processedThrough.Value.UtcDateTime)
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        if (filesToProcess.Count == 0)
        {
            _logger.LogInformation("Journal backfill: nothing new to process");
            return;
        }

        _logger.LogInformation("Backfilling {Count} journal files", filesToProcess.Count);

        // Suppress store notifications during backfill: replaying years of
        // history would otherwise spam SignalR pushes that are immediately
        // superseded. The frontend fetches a full snapshot once it connects.
        _store.SuppressNotifications = true;
        try
        {
            var totalLines = 0;
            var maxProcessedWriteUtc = DateTime.MinValue;
            foreach (var file in filesToProcess)
            {
                totalLines += await BackfillFileAsync(file, ct);
                if (file.LastWriteTimeUtc > maxProcessedWriteUtc)
                {
                    maxProcessedWriteUtc = file.LastWriteTimeUtc;
                }
            }

            await UpdateHighWaterMarkAsync(maxProcessedWriteUtc, ct);
            _logger.LogInformation("Journal backfill complete: {Files} files, {Lines} lines", filesToProcess.Count, totalLines);
        }
        finally
        {
            _store.SuppressNotifications = false;
        }
    }

    private async Task<DateTimeOffset?> LoadHighWaterMarkAsync(CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var mark = await db.AppSettings.FindAsync([ProcessedThroughSettingKey], ct);
        if (mark is not null &&
            DateTimeOffset.TryParse(mark.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        var legacy = await db.AppSettings.FindAsync([LegacyCompleteSettingKey], ct);
        if (legacy is not null &&
            DateTimeOffset.TryParse(legacy.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var legacyParsed))
        {
            return legacyParsed;
        }

        return null;
    }

    private async Task<int> BackfillFileAsync(FileInfo file, CancellationToken ct)
    {
        _logger.LogInformation("Backfilling journal file {File}", file.Name);

        var lineNumber = 0;
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lineNumber++;
            _reconciler.Handle(new JournalLine(file.FullName, lineNumber, line));
        }

        return lineNumber;
    }

    private async Task UpdateHighWaterMarkAsync(DateTime throughUtc, CancellationToken ct)
    {
        // The write queue is FIFO: awaiting this one final write guarantees
        // every write enqueued by the lines above (via the reconciler's normal
        // fire-and-forget archiving/recording) has already completed.
        await _writeQueue.EnqueueAsync(async (db, innerCt) =>
        {
            var value = new DateTimeOffset(throughUtc, TimeSpan.Zero).ToString("O");
            var existing = await db.AppSettings.FindAsync([ProcessedThroughSettingKey], innerCt);
            if (existing is null)
            {
                db.AppSettings.Add(new AppSetting { Key = ProcessedThroughSettingKey, Value = value });
            }
            else
            {
                existing.Value = value;
            }

            await db.SaveChangesAsync(innerCt);
        });
    }
}
