using EliteDashboard.Data;
using EliteDashboard.Domain;
using EliteDashboard.Journal;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

// Regression coverage for a real bug: DashboardState.Fleet only ever came
// from replaying StoredShips/Loadout lines, but JournalBackfillService's
// high-water mark means a journal file is only ever replayed once - once the
// file containing the last (sparse, player-triggered) StoredShips visit ages
// out of that window, a plain process restart silently collapsed Fleet down
// to just the currently-flown ship, since StateStore itself never persists
// across restarts. The fix persists a Ship table snapshot on every
// StoredShips/Loadout reconciliation and seeds StateStore.Fleet from it at
// startup, independent of whether the source journal file still gets
// replayed.
public sealed class FleetPersistenceTests
{
    [Fact]
    public async Task Fleet_SurvivesRestart_EvenWhenSourceFileIsNeverReplayedAgain()
    {
        using var db = new TestDb();
        var queue = new DbWriteQueue();
        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            // "Session 1": a StoredShips visit reports a 3-ship fleet.
            var firstStore = new StateStore();
            var firstReconciler = CreateReconciler(firstStore, db, queue);
            var line = new JournalLine("Journal.session1.log", 1, """
                {"timestamp":"2026-01-01T00:00:00Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra",
                 "ShipsHere":[{"ShipID":1,"ShipType":"sidewinder","Value":1000}],
                 "ShipsRemote":[{"ShipID":2,"ShipType":"Vulture","Name":"Hunter","StarSystem":"Vequess","Value":2000},
                                 {"ShipID":3,"ShipType":"Type6","StarSystem":"Wolf 397","Value":3000}]}
                """);
            firstReconciler.Handle(line);

            // The fleet sync is fire-and-forget through the write queue -
            // wait for the Ship rows to actually land before moving on.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var shipCount = 0;
            while (!cts.IsCancellationRequested && shipCount < 3)
            {
                await Task.Delay(50);
                await using var check = db.CreateContext();
                shipCount = await check.Ships.CountAsync();
            }

            Assert.Equal(3, shipCount);

            // "Restart": a brand new StateStore, with nothing replayed - this
            // is the scenario where the old bug would leave Fleet empty.
            var secondStore = new StateStore();
            var secondReconciler = CreateReconciler(secondStore, db, queue);
            await secondReconciler.SeedFleetFromDatabaseAsync();

            Assert.Equal(3, secondStore.Current.Fleet.Count);
            Assert.Contains(secondStore.Current.Fleet, s => s.ShipId == 1 && s.ShipType == "sidewinder");
            Assert.Contains(secondStore.Current.Fleet, s => s.ShipId == 2 && s.Name == "Hunter");
            Assert.Contains(secondStore.Current.Fleet, s => s.ShipId == 3 && s.ShipType == "Type6");
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PersistedFleet_ReflectsShipsSoldSinceTheLastSync()
    {
        using var db = new TestDb();
        var queue = new DbWriteQueue();
        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            var store = new StateStore();
            var reconciler = CreateReconciler(store, db, queue);

            reconciler.Handle(new JournalLine("Journal.test.log", 1, """
                {"timestamp":"2026-01-01T00:00:00Z","event":"StoredShips","StationName":"A","StarSystem":"A",
                 "ShipsHere":[{"ShipID":1,"ShipType":"sidewinder","Value":1000}],
                 "ShipsRemote":[{"ShipID":2,"ShipType":"Vulture","StarSystem":"B","Value":2000}]}
                """));

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var count = 0;
                while (!cts.IsCancellationRequested && count < 2)
                {
                    await Task.Delay(50);
                    await using var check = db.CreateContext();
                    count = await check.Ships.CountAsync();
                }

                Assert.Equal(2, count);
            }

            // Ship 2 sold - a later StoredShips visit reports only ship 1.
            reconciler.Handle(new JournalLine("Journal.test.log", 2, """
                {"timestamp":"2026-01-01T00:01:00Z","event":"StoredShips","StationName":"A","StarSystem":"A",
                 "ShipsHere":[{"ShipID":1,"ShipType":"sidewinder","Value":1000}],
                 "ShipsRemote":[]}
                """));

            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var finalCount = -1;
            while (!cts2.IsCancellationRequested && finalCount != 1)
            {
                await Task.Delay(50);
                await using var check = db.CreateContext();
                finalCount = await check.Ships.CountAsync();
            }

            Assert.Equal(1, finalCount);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    // Renaming a ship in game emits no event this app models, so a new name
    // only ever reaches us on the next Loadout. That updated
    // DashboardState.ShipName (which the dashboard reads) but not the matching
    // fleet entry (which the fleet page reads), so the two views disagreed
    // about the current ship's name until the next shipyard visit rebuilt the
    // fleet from a StoredShips event.
    [Fact]
    public void LoadoutEvent_RefreshesTheNameOfTheShipAlreadyInTheFleet()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var reconciler = CreateReconciler(store, db, new DbWriteQueue());

        reconciler.Handle(new JournalLine("Journal.test.log", 1, """
            {"timestamp":"2026-01-01T00:00:00Z","event":"StoredShips","StationName":"A","StarSystem":"A",
             "ShipsHere":[{"ShipID":7,"ShipType":"krait_mkii","Name":"Old Name","Value":1000}],
             "ShipsRemote":[]}
            """));

        Assert.Equal("Old Name", store.Current.Fleet.Single(s => s.ShipId == 7).Name);

        reconciler.Handle(new JournalLine("Journal.test.log", 2, """
            {"timestamp":"2026-01-01T00:01:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":7,"ShipName":"New Name","ShipIdent":"KR-01","CargoCapacity":180,"MaxJumpRange":30.5,"Rebuy":19000,"HullValue":380000,"ModulesValue":120000,"Modules":[]}
            """));

        var current = store.Current.Fleet.Single(s => s.ShipId == 7);
        Assert.Equal("New Name", current.Name);
        Assert.Equal(ShipLocationKind.Current, current.Location);

        // The dashboard's copy of the name was always correct - the point is
        // that the fleet entry now agrees with it.
        Assert.Equal("New Name", store.Current.ShipName);
    }

    private static JournalStateReconciler CreateReconciler(StateStore store, TestDb db, DbWriteQueue queue)
        => new(
            store,
            queue,
            new TransactionFeed(),
            new TradeRoutePlanService(db.Factory),
            db.Factory,
            NullLogger<JournalStateReconciler>.Instance);
}
