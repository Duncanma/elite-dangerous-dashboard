using EliteDashboard.Journal.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.Journal;

/// <summary>
/// Owns the JournalTailer's and snapshot-file watchers' lifecycles and wires
/// their output into the JournalStateReconciler. This is the only piece the
/// Host needs to register.
/// </summary>
public sealed class JournalIngestionService : BackgroundService
{
    private readonly JournalTailer _tailer;
    private readonly JournalStateReconciler _reconciler;
    private readonly JournalBackfillService _backfillService;
    private readonly ILogger<JournalIngestionService> _logger;
    private SnapshotWatcher<StatusFileSnapshot>? _statusWatcher;

    public JournalIngestionService(
        JournalTailer tailer, JournalStateReconciler reconciler, JournalBackfillService backfillService,
        ILogger<JournalIngestionService> logger)
    {
        _tailer = tailer;
        _reconciler = reconciler;
        _backfillService = backfillService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Must happen before any replay: DashboardState.Fleet only ever comes
        // from replaying StoredShips/Loadout lines, and JournalBackfillService's
        // high-water mark means old files aren't replayed forever - this
        // restores whatever the durable Ship table already knows as a
        // baseline, so a restart can't silently lose fleet membership.
        await _reconciler.SeedFleetFromDatabaseAsync(stoppingToken);

        _tailer.LineRead += _reconciler.Handle;

        // Replays completed journal files before the live tailer takes over
        // the current one: years of history on first run, then just the
        // sessions played while the app was offline (tracked via a
        // high-water-mark AppSetting). This doesn't block the Host from
        // accepting requests (BackgroundService.ExecuteAsync running long is
        // expected), just means history fills in progressively during
        // startup.
        await _backfillService.BackfillIfNeededAsync(stoppingToken);

        _tailer.Start();

        _statusWatcher = new SnapshotWatcher<StatusFileSnapshot>(_tailer.JournalDirectory, "Status.json", _logger);
        _statusWatcher.Changed += _reconciler.HandleStatusSnapshot;
        _statusWatcher.Start();

        _logger.LogInformation("Journal ingestion started");

        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _tailer.LineRead -= _reconciler.Handle;
        _tailer.Stop();
        _statusWatcher?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
