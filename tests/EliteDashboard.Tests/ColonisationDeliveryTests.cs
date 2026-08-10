using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Journal;
using EliteDashboard.Journal.Events;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

/// <summary>
/// ColonisationContribution carries a Contributions array (commodity +
/// tonnage). That payload must be persisted as delivery rows — not folded
/// into Transaction (zero-credit cargo) — and must be replay-safe. See #7.
/// </summary>
public sealed class ColonisationDeliveryTests
{
    private const string ContributionJson =
        """{"timestamp":"2026-07-09T22:01:17Z","event":"ColonisationContribution","MarketID":3966562562,"Contributions":[{"Name":"$FruitAndVegetables_name;","Name_Localised":"Fruit and Vegetables","Amount":50},{"Name":"$Water_name;","Name_Localised":"Water","Amount":741}]}""";

    [Fact]
    public void Parser_ReadsContributionsArray()
    {
        var parsed = JournalEventParser.Parse(ContributionJson);
        var e = Assert.IsType<ColonisationContributionEvent>(parsed);
        Assert.Equal(3966562562, e.MarketID);
        Assert.Equal(2, e.Contributions.Count);
        Assert.Equal("$FruitAndVegetables_name;", e.Contributions[0].Name);
        Assert.Equal("Fruit and Vegetables", e.Contributions[0].NameLocalised);
        Assert.Equal(50, e.Contributions[0].Amount);
        Assert.Equal(741, e.Contributions[1].Amount);
    }

    [Fact]
    public async Task Contribution_PersistsOneDeliveryRowPerCommodity()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = CreateReconciler(store, db, queue);

        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            reconciler.Handle(new JournalLine("Journal.test.log", 10, ContributionJson));

            var rows = await WaitFor(() => CountDeliveries(db, 3966562562), n => n >= 2);
            Assert.Equal(2, rows);

            await using var check = db.CreateContext();
            var deliveries = await check.ColonisationDeliveries.AsNoTracking()
                .Where(d => d.MarketId == 3966562562)
                .OrderBy(d => d.CommodityName)
                .ToListAsync();

            Assert.Equal(2, deliveries.Count);
            Assert.Contains(deliveries, d => d.CommodityDisplayName == "Fruit and Vegetables" && d.Amount == 50);
            Assert.Contains(deliveries, d => d.CommodityDisplayName == "Water" && d.Amount == 741);
            Assert.All(deliveries, d =>
            {
                Assert.Equal("Journal.test.log", d.JournalFile);
                Assert.Equal(10, d.LineNumber);
                Assert.Equal(new DateTime(2026, 7, 9, 22, 1, 17, DateTimeKind.Utc), d.TimestampUtc);
            });

            var site = await check.ColonisationSites.SingleAsync(s => s.MarketId == 3966562562);
            Assert.True(site.HasContributed);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Contribution_Replay_DoesNotDuplicateDeliveries()
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = CreateReconciler(store, db, queue);

        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            var line = new JournalLine("Journal.test.log", 10, ContributionJson);
            reconciler.Handle(line);
            await WaitFor(() => CountDeliveries(db, 3966562562), n => n >= 2);

            reconciler.Handle(line);
            // Marker write so the second enqueue has drained.
            await queue.EnqueueAsync(async (ctx, ct) =>
            {
                ctx.AppSettings.Add(new AppSetting { Key = $"marker-{Guid.NewGuid():N}", Value = "1" });
                await ctx.SaveChangesAsync(ct);
            });

            await using var check = db.CreateContext();
            Assert.Equal(2, await check.ColonisationDeliveries.CountAsync(d => d.MarketId == 3966562562));
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<int> CountDeliveries(TestDb db, long marketId)
    {
        await using var check = db.CreateContext();
        return await check.ColonisationDeliveries.CountAsync(d => d.MarketId == marketId);
    }

    private static async Task<int> WaitFor(Func<Task<int>> query, Func<int, bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            var value = await query();
            if (predicate(value))
            {
                return value;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for colonisation deliveries.");
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
