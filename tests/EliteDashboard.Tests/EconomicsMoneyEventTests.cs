using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Journal;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

/// <summary>
/// Credit-moving journal events that were previously dropped (outfitting,
/// vehicle restock, crew hire, etc.) must land in the Economics ledger as
/// a single idempotent Transaction per journal line.
/// </summary>
public sealed class EconomicsMoneyEventTests
{
    [Fact]
    public async Task ModuleBuy_WithTradeIn_RecordsNetOutfittingCost()
    {
        // BuyPrice 20195 - SellPrice 8078 = net 12117 paid.
        await AssertTransactionAsync(
            """{"timestamp":"2026-05-15T19:19:26Z","event":"ModuleBuy","Slot":"Slot05_Size1","SellItem_Localised":"Shield Cell Bank","SellPrice":8078,"BuyItem_Localised":"Shield Cell Bank","BuyPrice":20195,"Ship":"vulture","ShipID":4}""",
            TransactionCategory.Outfitting,
            -12117,
            descriptionContains: "Shield Cell Bank");
    }

    [Fact]
    public async Task ModuleBuy_WithoutTradeIn_RecordsFullBuyPrice()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-05-15T19:17:24Z","event":"ModuleBuy","Slot":"Armour","BuyItem_Localised":"Military Grade Composite","BuyPrice":4433053,"Ship":"vulture","ShipID":4}""",
            TransactionCategory.Outfitting,
            -4433053,
            descriptionContains: "Military Grade Composite");
    }

    [Fact]
    public async Task ModuleSell_RecordsSale()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-05-17T00:03:52Z","event":"ModuleSell","SellItem_Localised":"K-Warrant Scanner","SellPrice":1097095,"Ship":"diamondbackxl","ShipID":5}""",
            TransactionCategory.Outfitting,
            1097095,
            descriptionContains: "K-Warrant Scanner");
    }

    [Fact]
    public async Task ModuleBuyAndStore_RecordsBuyPrice()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-07-19T21:22:23Z","event":"ModuleBuyAndStore","BuyItem_Localised":"Mk II Vessel Hangar","BuyPrice":794387,"Ship":"mediumtransport01","ShipID":8}""",
            TransactionCategory.Outfitting,
            -794387,
            descriptionContains: "Mk II Vessel Hangar");
    }

    [Fact]
    public async Task RestockVehicle_RecordsCost()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-06-22T20:11:11Z","event":"RestockVehicle","Type":"empire_fighter","Type_Localised":"Gu-97","Cost":15270,"Count":1}""",
            TransactionCategory.VehicleRestock,
            -15270,
            descriptionContains: "Gu-97");
    }

    [Fact]
    public async Task CrewHire_RecordsCost()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-06-22T20:07:04Z","event":"CrewHire","Name":"Renato Sutton","Cost":15000}""",
            TransactionCategory.CrewHire,
            -15000,
            descriptionContains: "Renato Sutton");
    }

    [Fact]
    public async Task SellDrones_RecordsTotalSale()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-08-07T18:29:45Z","event":"SellDrones","Type":"Drones","Count":15,"SellPrice":100,"TotalSale":1500}""",
            TransactionCategory.Rearm,
            1500,
            descriptionContains: "drones");
    }

    [Fact]
    public async Task RefuelPartial_RecordsAsRefuel()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-06-26T00:42:34Z","event":"RefuelPartial","Cost":43,"Amount":0.857}""",
            TransactionCategory.Refuel,
            -43,
            descriptionContains: "Refuel");
    }

    [Fact]
    public async Task BuyExplorationData_RecordsCost()
    {
        await AssertTransactionAsync(
            """{"timestamp":"2026-07-06T18:19:36Z","event":"BuyExplorationData","System":"Vequess","Cost":40}""",
            TransactionCategory.Other,
            -40,
            descriptionContains: "Vequess");
    }

    private static async Task AssertTransactionAsync(
        string json, TransactionCategory category, long amount, string descriptionContains)
    {
        using var db = new TestDb();
        var store = new StateStore();
        var queue = new DbWriteQueue();
        var reconciler = new JournalStateReconciler(
            store,
            queue,
            new TransactionFeed(),
            new TradeRoutePlanService(db.Factory),
            db.Factory,
            NullLogger<JournalStateReconciler>.Instance);

        using var writer = new DbWriterService(queue, db.Factory, NullLogger<DbWriterService>.Instance);
        await writer.StartAsync(CancellationToken.None);
        try
        {
            reconciler.Handle(new JournalLine("Journal.test.log", 42, json));

            var row = await WaitForTransaction(db);
            Assert.Equal(category, row.Category);
            Assert.Equal(amount, row.Amount);
            Assert.Contains(descriptionContains, row.Description ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(amount, store.Current.Economy.Credits);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<Transaction> WaitForTransaction(TestDb db)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            await using var check = db.CreateContext();
            var row = await check.Transactions.AsNoTracking().SingleOrDefaultAsync();
            if (row is not null)
            {
                return row;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for transaction write.");
    }
}
