using System.Text.Json;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Journal;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

// Every Loadout event is a full snapshot of the ship being flown - persisting
// the latest one per ship is what lets the fleet page show a stored ship's
// modules/cargo/jump-range from the last time it was boarded. The upsert must
// be replay-safe (older events never clobber a newer snapshot) and follow the
// fleet (a sold ship's snapshot goes away with it).
public sealed class ShipLoadoutPersistenceTests
{
    [Fact]
    public async Task LoadoutEvent_PersistsSnapshotWithModulesAndShipFigures()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = CreateReconciler(store, db, queue);

        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            reconciler.Handle(new JournalLine("Journal.test.log", 1,
                """{"timestamp":"2026-01-02T10:00:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":7,"CargoCapacity":180,"MaxJumpRange":30.52,"FuelCapacity":{"Main":32.0,"Reserve":0.5},"UnladenMass":350.8,"Rebuy":19000000,"HullValue":380000000,"ModulesValue":120000000,"Modules":[{"Slot":"LargeHardpoint1","Item":"hpt_beamlaser_fixed_large","On":true,"Priority":1,"Health":1.0},{"Slot":"LifeSupport","Item":"int_lifesupport_size5_class_a","On":true,"Priority":0,"Health":1.0}]}"""));

            var row = await WaitForLoadout(db, shipId: 7);

            Assert.Equal("krait_mkii", row.ShipType);
            Assert.Equal(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), row.SeenAtUtc);
            Assert.Equal(180, row.CargoCapacity);
            Assert.Equal(30.52, row.MaxJumpRange);
            Assert.Equal(32.0, row.FuelCapacityMain);
            Assert.Equal(350.8, row.UnladenMass);
            Assert.Equal(19000000, row.Rebuy);
            Assert.Equal(380000000, row.HullValue);
            Assert.Equal(120000000, row.ModulesValue);

            var modules = JsonSerializer.Deserialize<List<ShipModuleSummary>>(row.ModulesJson);
            Assert.NotNull(modules);
            Assert.Equal(2, modules.Count);
            Assert.Equal("LargeHardpoint1", modules[0].Slot);
            Assert.Equal("hpt_beamlaser_fixed_large", modules[0].Item);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task LoadoutEvent_OlderTimestamp_DoesNotOverwriteNewerSnapshot()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = CreateReconciler(store, db, queue);

        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            reconciler.Handle(new JournalLine("Journal.test.log", 1,
                """{"timestamp":"2026-01-02T10:00:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":7,"MaxJumpRange":30.5,"Modules":[]}"""));
            await WaitForLoadout(db, shipId: 7);

            // An older Loadout replayed after the fact (e.g. journal history
            // reprocessing) must leave the newer snapshot alone.
            reconciler.Handle(new JournalLine("Journal.test.log", 2,
                """{"timestamp":"2026-01-01T09:00:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":7,"MaxJumpRange":25.0,"Modules":[]}"""));

            // FIFO write queue: once this later marker write is visible, the
            // no-op write above has also been processed.
            reconciler.Handle(new JournalLine("Journal.test.log", 3,
                """{"timestamp":"2026-01-03T08:00:00Z","event":"Loadout","Ship":"sidewinder","ShipID":8,"Modules":[]}"""));
            await WaitForLoadout(db, shipId: 8);

            await using var check = db.CreateContext();
            var row = await check.ShipLoadouts.SingleAsync(l => l.ShipId == 7);
            Assert.Equal(new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc), row.SeenAtUtc);
            Assert.Equal(30.5, row.MaxJumpRange);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task FleetResync_PrunesLoadoutsForShipsNoLongerInFleet()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = CreateReconciler(store, db, queue);

        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            reconciler.Handle(new JournalLine("Journal.test.log", 1,
                """{"timestamp":"2026-01-02T10:00:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":7,"Modules":[]}"""));
            reconciler.Handle(new JournalLine("Journal.test.log", 2,
                """{"timestamp":"2026-01-02T11:00:00Z","event":"Loadout","Ship":"sidewinder","ShipID":8,"Modules":[]}"""));
            await WaitForLoadout(db, shipId: 8);

            // Ship 7 is gone from the fleet (sold) - the next StoredShips
            // resync lists nothing but the current ship.
            reconciler.Handle(new JournalLine("Journal.test.log", 3,
                """{"timestamp":"2026-01-02T12:00:00Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","ShipsHere":[],"ShipsRemote":[]}"""));

            // The fleet resync and the prune are one queued write - when the
            // Ships table reflects the resync, the prune has landed too.
            await WaitFor(db, async check =>
                await check.Ships.SingleOrDefaultAsync(s => s.ShipId == 8) is { Location: "Current" } ship ? ship : null);

            await using var verify = db.CreateContext();
            Assert.False(await verify.ShipLoadouts.AnyAsync(l => l.ShipId == 7));
            Assert.True(await verify.ShipLoadouts.AnyAsync(l => l.ShipId == 8));
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<ShipLoadout> WaitForLoadout(TestDb db, int shipId)
    {
        return await WaitFor(db, check => check.ShipLoadouts.SingleOrDefaultAsync(l => l.ShipId == shipId));
    }

    private static async Task<T> WaitFor<T>(TestDb db, Func<DashboardDbContext, Task<T?>> query) where T : class
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            await using var check = db.CreateContext();
            if (await query(check) is { } result)
            {
                return result;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("Timed out waiting for the queued write to land.");
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
