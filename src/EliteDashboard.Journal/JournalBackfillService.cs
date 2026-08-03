using System.Text;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.Journal;

/// <summary>
/// One-time import of journal files older than the current session. The live
/// JournalTailer only ever reads the current/latest file (needed to rebuild
/// in-memory state from scratch on every restart) - without this, years of
/// real history sitting in Saved Games would never reach the Economics and
/// Powerplay charts. Tracked via an AppSetting flag so years of files aren't
/// re-scanned on every subsequent startup.
/// </summary>
public sealed class JournalBackfillService
{
    private const string BackfillCompleteSettingKey = "Journal.BackfillCompleteUtc";

    private readonly JournalOptions _options;
    private readonly JournalStateReconciler _reconciler;
    private readonly DbWriteQueue _writeQueue;
    private readonly StateStore _store;
    private readonly IDbContextFactory<DashboardDbContext> _dbContextFactory;
    private readonly ILogger<JournalBackfillService> _logger;

    public JournalBackfillService(
        JournalOptions options, JournalStateReconciler reconciler, DbWriteQueue writeQueue,
        StateStore store, IDbContextFactory<DashboardDbContext> dbContextFactory, ILogger<JournalBackfillService> logger)
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
        await using (var db = await _dbContextFactory.CreateDbContextAsync(ct))
        {
            if (await db.AppSettings.FindAsync([BackfillCompleteSettingKey], ct) is not null)
            {
                return;
            }
        }

        var directory = JournalFileSetLocator.ResolveJournalDirectory(_options.JournalPathOverride);
        if (!Directory.Exists(directory))
        {
            return;
        }

        // The current/latest file is excluded here - JournalTailer replays
        // that one itself (and needs to keep doing so on every restart to
        // rebuild live state), so backfilling it too would just be redundant
        // work protected by the same dedup keys either way.
        var latest = JournalFileSetLocator.FindLatestJournalFile(directory);
        var historicalFiles = new DirectoryInfo(directory)
            .GetFiles("Journal.*.log")
            .Where(f => latest is null || !string.Equals(f.FullName, latest.FullName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        if (historicalFiles.Count == 0)
        {
            await MarkCompleteAsync(ct);
            return;
        }

        _logger.LogInformation("Backfilling {Count} historical journal files (one-time)", historicalFiles.Count);

        // Historical data should never make an already-open dashboard
        // flicker through years of the past - JournalTailer's own replay of
        // the current file (right after this) leaves Current fully correct.
        _store.SuppressNotifications = true;
        try
        {
            var totalLines = 0;
            foreach (var file in historicalFiles)
            {
                totalLines += await BackfillFileAsync(file, ct);
            }

            // DbWriteQueue is strictly FIFO/serial - awaiting one final write
            // here guarantees every write enqueued by the lines above (via
            // the reconciler's normal fire-and-forget archiving/recording)
            // has already completed before this method returns.
            await MarkCompleteAsync(ct);
            _logger.LogInformation("Historical journal backfill complete: {Files} files, {Lines} lines", historicalFiles.Count, totalLines);
        }
        finally
        {
            _store.SuppressNotifications = false;
        }
    }

    private async Task<int> BackfillFileAsync(FileInfo file, CancellationToken ct)
    {
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var lineNumber = 0L;
        string? rawLine;
        while ((rawLine = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == '﻿')
            {
                line = line[1..];
            }

            _reconciler.Handle(new JournalLine(file.FullName, lineNumber, line));
        }

        return (int)lineNumber;
    }

    private async Task MarkCompleteAsync(CancellationToken ct)
    {
        await _writeQueue.EnqueueAsync(async (db, innerCt) =>
        {
            db.AppSettings.Add(new AppSetting { Key = BackfillCompleteSettingKey, Value = DateTimeOffset.UtcNow.ToString("O") });
            await db.SaveChangesAsync(innerCt);
        });
    }
}
