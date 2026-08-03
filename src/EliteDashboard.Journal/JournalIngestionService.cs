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
        _tailer.LineRead += _reconciler.Handle;

        // Runs once ever (tracked via an AppSetting flag) - importing years
        // of historical journal files before the live tailer takes over the
        // current one. This doesn't block the Host from accepting requests
        // (BackgroundService.ExecuteAsync running long is expected), just
        // means Economics/Powerplay history fills in progressively on the
        // very first startup after this feature shipped.
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
