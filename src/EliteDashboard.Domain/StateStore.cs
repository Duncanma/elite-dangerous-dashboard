namespace EliteDashboard.Domain;

/// <summary>
/// Thread-safe holder for the single reconciled DashboardState, shared between
/// the journal ingestion pipeline (writer) and API/SignalR (readers).
/// </summary>
public sealed class StateStore
{
    private readonly Lock _lock = new();
    private DashboardState _current = DashboardState.Empty;

    public event Action<DashboardState>? Changed;

    /// <summary>
    /// While true, Update still applies but skips the Changed notification -
    /// used while replaying years of historical journal backfill so
    /// connected browsers don't see the dashboard flicker through six years
    /// of history. The live journal tailer's own replay (which starts right
    /// after backfill finishes) naturally leaves Current fully correct.
    /// </summary>
    public bool SuppressNotifications { get; set; }

    public DashboardState Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void Update(Func<DashboardState, DashboardState> updater)
    {
        DashboardState updated;
        lock (_lock)
        {
            updated = updater(_current);
            _current = updated;
        }

        if (!SuppressNotifications)
        {
            Changed?.Invoke(updated);
        }
    }
}
