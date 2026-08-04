using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.External;
using EliteDashboard.Trading;
using Microsoft.Extensions.Logging.Abstractions;

namespace EliteDashboard.Tests;

// EDDN prices decay fast - a weeks-old quote points at markets that no longer
// exist. The route engine must ignore quotes older than the same 3-day cutoff
// the colonisation shipping planner already applies.
public sealed class TradeRouteEngineStalenessTests
{
    [Fact]
    public async Task SingleHop_SellQuoteOlderThanTheCutoff_ProducesNoRoute()
    {
        using var db = new TestDb();
        var (stationA, stationB, commodity) = SeedNetwork(db);
        SeedQuote(db, stationA, commodity, buyPrice: 100, sellPrice: 0, supply: 1000, demand: 0, age: TimeSpan.FromHours(1));
        SeedQuote(db, stationB, commodity, buyPrice: 0, sellPrice: 1000, supply: 0, demand: 1000, age: TimeSpan.FromDays(30));

        var engine = CreateEngine(db);
        var candidates = await engine.FindSingleHopRoutesAsync(Criteria());

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task SingleHop_FreshQuotes_StillProduceRoutes()
    {
        using var db = new TestDb();
        var (stationA, stationB, commodity) = SeedNetwork(db);
        SeedQuote(db, stationA, commodity, buyPrice: 100, sellPrice: 0, supply: 1000, demand: 0, age: TimeSpan.FromHours(1));
        SeedQuote(db, stationB, commodity, buyPrice: 0, sellPrice: 1000, supply: 0, demand: 1000, age: TimeSpan.FromHours(2));

        var engine = CreateEngine(db);
        var candidates = await engine.FindSingleHopRoutesAsync(Criteria());

        var candidate = Assert.Single(candidates);
        Assert.Equal(stationA.Id, candidate.OriginStationId);
        Assert.Equal(stationB.Id, candidate.DestinationStationId);
        Assert.Equal(900 * 100, candidate.TotalProfit);
    }

    private static TradeRouteSearchCriteria Criteria() => new()
    {
        CargoCapacity = 100,
        Credits = 1_000_000,
        MaxHops = 1,
        ShipJumpRangeLy = 0,
        MaxJumpsFromOrigin = 0,
        MaxJumpsBetweenStations = 0,
        ExcludePlanetary = false,
        ExcludeFleetCarriers = false,
    };

    private static TradeRouteEngine CreateEngine(TestDb db) => new(
        db.Factory,
        new SystemCoordinateResolver(
            new DbWriteQueue(),
            new EdsmClient(new StubHttpClientFactory(), NullLogger<EdsmClient>.Instance),
            NullLogger<SystemCoordinateResolver>.Instance));

    // The resolver is never exercised in these tests (every seeded system has
    // coordinates), it just needs a constructible EdsmClient.
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static (Station A, Station B, Commodity Commodity) SeedNetwork(TestDb db)
    {
        using var ctx = db.CreateContext();
        var systemA = new StarSystem { Name = "Alpha", HasCoordinates = true, X = 0, Y = 0, Z = 0 };
        var systemB = new StarSystem { Name = "Beta", HasCoordinates = true, X = 10, Y = 0, Z = 0 };
        ctx.StarSystems.AddRange(systemA, systemB);
        ctx.SaveChanges();

        var stationA = new Station { MarketId = 1001, Name = "Alpha Port", StarSystemId = systemA.Id, StationType = "Orbis" };
        var stationB = new Station { MarketId = 1002, Name = "Beta Port", StarSystemId = systemB.Id, StationType = "Orbis" };
        ctx.Stations.AddRange(stationA, stationB);

        var commodity = new Commodity { InternalSymbol = "gold", DisplayName = "Gold" };
        ctx.Commodities.Add(commodity);
        ctx.SaveChanges();

        return (stationA, stationB, commodity);
    }

    private static void SeedQuote(TestDb db, Station station, Commodity commodity,
        long buyPrice, long sellPrice, long supply, long demand, TimeSpan age)
    {
        using var ctx = db.CreateContext();
        ctx.MarketPriceQuotes.Add(new MarketPriceQuote
        {
            StationId = station.Id,
            CommodityId = commodity.Id,
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            Supply = supply,
            Demand = demand,
            CollectedAtUtc = DateTime.UtcNow - age,
        });
        ctx.SaveChanges();
    }
}
