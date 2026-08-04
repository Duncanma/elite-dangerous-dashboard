namespace EliteDashboard.Data.Entities;

/// <summary>
/// Durable snapshot of the commander's owned fleet, kept in sync from
/// StoredShips (full resync - the event lists the complete fleet every time)
/// and Loadout (current-ship-only) events.
///
/// Exists because DashboardState.Fleet would otherwise be a pure in-memory
/// replay artifact with no persistence of its own. JournalBackfillService's
/// high-water mark means a journal file is only ever replayed once - fine for
/// state that's also written to a durable table (dedup keys make replays
/// cheap no-ops), but StoredShips is a sparse, player-triggered event (opening
/// a shipyard's Ships panel), not part of the journal's per-session startup
/// block the way Rank/Statistics/etc. are. Once the file containing the last
/// StoredShips visit ages out of the backfill window, an in-memory-only Fleet
/// would silently collapse to just the currently-flown ship on the next
/// process restart. This table is seeded into StateStore.Fleet at startup
/// (JournalStateReconciler.SeedFleetFromDatabaseAsync) specifically to avoid that.
/// </summary>
public sealed class Ship
{
    public long Id { get; set; }
    public required int ShipId { get; set; }
    public string? ShipType { get; set; }
    public string? ShipTypeDisplay { get; set; }
    public string? Name { get; set; }
    public string? StarSystem { get; set; }
    public string? Station { get; set; }

    /// <summary>Mirrors Domain.ShipLocationKind ("Current"/"Here"/"Remote"/"InTransit") as a plain string, matching this codebase's convention for other "kind" fields (e.g. Station.LastUpdateSource).</summary>
    public required string Location { get; set; }

    public DateTime LastUpdatedUtc { get; set; }
}
