using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Journal;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

// System.Text.Json honors an explicit JSON null even when the record property
// has an initializer ("Modules": null deserializes to null, not []). Frontier's
// journal is inconsistent enough that known events have shown up with null
// arrays - the reconciler must treat them as empty, not crash the tailer.
public sealed class JournalStateReconcilerNullDataTests
{
    [Fact]
    public void Loadout_WithNullModules_UpdatesStateInsteadOfThrowing()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var reconciler = CreateReconciler(store, db, new DbWriteQueue());

        var line = new JournalLine("Journal.test.log", 1,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Loadout","Ship":"krait_mkii","ShipID":7,"Modules":null}""");

        var ex = Record.Exception(() => reconciler.Handle(line));

        Assert.Null(ex);
        Assert.Equal("krait_mkii", store.Current.ShipType);
        Assert.Empty(store.Current.CurrentShipModules);
    }

    [Fact]
    public void StoredShips_WithNullShipLists_UpdatesStateInsteadOfThrowing()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var reconciler = CreateReconciler(store, db, new DbWriteQueue());

        var line = new JournalLine("Journal.test.log", 1,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","ShipsHere":null,"ShipsRemote":null}""");

        var ex = Record.Exception(() => reconciler.Handle(line));

        Assert.Null(ex);
        Assert.NotNull(store.Current.Fleet);
    }

    [Fact]
    public async Task ColonisationDepot_WithNullResources_PersistsSiteWithNoRequirements()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = CreateReconciler(store, db, queue);

        // The depot upsert is fire-and-forget through the write queue, so run
        // the real writer to observe the outcome.
        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            var line = new JournalLine("Journal.test.log", 1,
                """{"timestamp":"2026-01-01T00:00:00Z","event":"ColonisationConstructionDepot","MarketID":12345,"ConstructionProgress":0.4,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":null}""");
            reconciler.Handle(line);

            ColonisationSite? site = null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!cts.IsCancellationRequested && site is null)
            {
                await Task.Delay(50);
                await using var check = db.CreateContext();
                site = await check.ColonisationSites
                    .Include(s => s.Resources)
                    .FirstOrDefaultAsync(s => s.MarketId == 12345);
            }

            Assert.NotNull(site);
            Assert.Empty(site.Resources);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
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
