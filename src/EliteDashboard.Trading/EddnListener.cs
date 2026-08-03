using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace EliteDashboard.Trading;

/// <summary>
/// Subscribes to EDDN's public ZeroMQ firehose for live, galaxy-wide
/// commodity prices. This is a standing subscription, not a periodic fetch -
/// it's what makes the trade planner "always fresh" without ever needing to
/// proactively refresh commodity prices.
///
/// The blocking receive loop runs on its own dedicated thread rather than a
/// pooled BackgroundService task, since a sustained blocking receive would
/// otherwise starve the thread pool.
/// </summary>
public sealed class EddnListener : BackgroundService
{
    private const string EddnAddress = "tcp://eddn.edcd.io:9500";

    private readonly DbWriteQueue _writeQueue;
    private readonly ILogger<EddnListener> _logger;

    public DateTime? LastMessageAtUtc { get; private set; }
    public long MessagesReceived { get; private set; }
    public long CommodityMessagesProcessed { get; private set; }

    public EddnListener(DbWriteQueue writeQueue, ILogger<EddnListener> logger)
    {
        _writeQueue = writeQueue;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var thread = new Thread(() => ReceiveLoop(stoppingToken))
        {
            IsBackground = true,
            Name = "EddnListener",
        };
        thread.Start();

        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        return tcs.Task;
    }

    private void ReceiveLoop(CancellationToken stoppingToken)
    {
        try
        {
            using var subscriber = new SubscriberSocket();
            subscriber.Options.ReceiveHighWatermark = 1000;
            subscriber.Connect(EddnAddress);
            subscriber.SubscribeToAnyTopic();
            _logger.LogInformation("EDDN listener connected to {Address}", EddnAddress);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!subscriber.TryReceiveFrameBytes(TimeSpan.FromSeconds(1), out var compressed))
                {
                    continue;
                }

                MessagesReceived++;
                LastMessageAtUtc = DateTime.UtcNow;

                try
                {
                    ProcessMessage(Decompress(compressed));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to process an EDDN message; skipping it");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EDDN listener stopped unexpectedly");
        }
    }

    private static string Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private void ProcessMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var schemaRef = root.TryGetProperty("$schemaRef", out var schemaEl) ? schemaEl.GetString() : null;
        if (schemaRef is null || !schemaRef.Contains("commodity", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!root.TryGetProperty("message", out var message))
        {
            return;
        }

        var systemName = GetString(message, "systemName");
        var stationName = GetString(message, "stationName");
        var stationType = GetString(message, "stationType");
        var marketId = message.TryGetProperty("marketId", out var m) && m.TryGetInt64(out var mid) ? mid : 0;

        if (systemName is null || stationName is null || marketId == 0)
        {
            return;
        }

        if (!message.TryGetProperty("commodities", out var commoditiesEl) || commoditiesEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var quotes = new List<CommodityQuote>();
        foreach (var c in commoditiesEl.EnumerateArray())
        {
            var name = GetString(c, "name");
            if (name is null)
            {
                continue;
            }

            quotes.Add(new CommodityQuote(
                name,
                GetLong(c, "buyPrice"),
                GetLong(c, "sellPrice"),
                GetLong(c, "demand"),
                GetLong(c, "stock")));
        }

        if (quotes.Count == 0)
        {
            return;
        }

        CommodityMessagesProcessed++;
        _ = _writeQueue.EnqueueAsync((db, ct) => UpsertAsync(db, systemName, stationName, stationType, marketId, quotes, ct));
    }

    private static async Task UpsertAsync(DashboardDbContext db, string systemName, string stationName, string? stationType, long marketId, List<CommodityQuote> quotes, CancellationToken ct)
    {
        var system = await db.StarSystems.FirstOrDefaultAsync(s => s.Name == systemName, ct);
        if (system is null)
        {
            system = new StarSystem { Name = systemName, LastUpdatedUtc = DateTime.UtcNow };
            db.StarSystems.Add(system);
            await db.SaveChangesAsync(ct);
        }

        var station = await db.Stations.FirstOrDefaultAsync(s => s.MarketId == marketId, ct);
        if (station is null)
        {
            station = new Station { MarketId = marketId, Name = stationName, StationType = stationType, StarSystemId = system.Id, LastUpdatedUtc = DateTime.UtcNow, LastUpdateSource = "EDDN" };
            db.Stations.Add(station);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            station.Name = stationName;
            // Never downgrade a StationType we already know - own Docked events
            // (and their exact LandingPads counts) are strictly more trustworthy
            // than EDDN's copy of the same field.
            station.StationType ??= stationType;
            station.LastUpdatedUtc = DateTime.UtcNow;
            station.LastUpdateSource = "EDDN";
        }

        foreach (var q in quotes)
        {
            var commodity = await db.Commodities.FirstOrDefaultAsync(c => c.InternalSymbol == q.Symbol, ct);
            if (commodity is null)
            {
                commodity = new Commodity { InternalSymbol = q.Symbol, DisplayName = q.Symbol };
                db.Commodities.Add(commodity);
                await db.SaveChangesAsync(ct);
            }

            var quote = await db.MarketPriceQuotes.FirstOrDefaultAsync(p => p.StationId == station.Id && p.CommodityId == commodity.Id, ct);
            if (quote is null)
            {
                db.MarketPriceQuotes.Add(new MarketPriceQuote
                {
                    StationId = station.Id,
                    CommodityId = commodity.Id,
                    BuyPrice = q.BuyPrice,
                    SellPrice = q.SellPrice,
                    Demand = q.Demand,
                    Supply = q.Supply,
                    CollectedAtUtc = DateTime.UtcNow,
                });
            }
            else
            {
                quote.BuyPrice = q.BuyPrice;
                quote.SellPrice = q.SellPrice;
                quote.Demand = q.Demand;
                quote.Supply = q.Supply;
                quote.CollectedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? GetString(JsonElement e, string prop) => e.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static long GetLong(JsonElement e, string prop) => e.TryGetProperty(prop, out var v) && v.TryGetInt64(out var l) ? l : 0;

    private sealed record CommodityQuote(string Symbol, long BuyPrice, long SellPrice, long Demand, long Supply);
}
