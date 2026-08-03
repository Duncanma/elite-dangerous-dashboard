using System.Text.Json;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>
/// Ship stats aren't in the journal for ships you don't own, so this pulls
/// them from two independent, real community sources and JOINS them - it
/// does not guess a mapping. EDCD/FDevIDs' shipyard.csv maps Frontier's
/// internal ship symbol (e.g. "DiamondBackXL") to the game's real display
/// name ("Diamondback Explorer"); EDCD/coriolis-data's ship files are keyed
/// by that same display name. Ships not yet catalogued in coriolis-data
/// (very recent additions) simply get no matching row - that's correct
/// behavior, not a bug, since we'd otherwise be fabricating stats.
///
/// Module names are simpler: the journal's Loadout event only ever gives a
/// module's internal symbol (e.g. "hpt_multicannon_gimbal_medium"), never a
/// display name, but EDCD/FDevIDs' outfitting.csv maps that symbol straight
/// to a real display name ("Multi-Cannon") in one file - no second source to
/// join against.
/// </summary>
public sealed class ReferenceDataImporter
{
    private const string ShipyardCsvUrl = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/shipyard.csv";
    private const string OutfittingCsvUrl = "https://raw.githubusercontent.com/EDCD/FDevIDs/master/outfitting.csv";
    private const string CoriolisShipListUrl = "https://api.github.com/repos/EDCD/coriolis-data/contents/ships";
    private const string CoriolisShipRawUrlTemplate = "https://raw.githubusercontent.com/EDCD/coriolis-data/master/ships/{0}";
    private const string LastImportSettingKey = "ShipSpecs.LastImportUtc";
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;
    private readonly DbWriteQueue _writeQueue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReferenceDataImporter> _logger;

    public ReferenceDataImporter(
        IDbContextFactory<DashboardDbContext> contextFactory, DbWriteQueue writeQueue,
        IHttpClientFactory httpClientFactory, ILogger<ReferenceDataImporter> logger)
    {
        _contextFactory = contextFactory;
        _writeQueue = writeQueue;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ImportIfStaleAsync(CancellationToken ct = default)
    {
        await using (var db = await _contextFactory.CreateDbContextAsync(ct))
        {
            var setting = await db.AppSettings.FindAsync([LastImportSettingKey], ct);
            var hasModuleSpecs = await db.ModuleStaticSpecs.AnyAsync(ct);
            if (setting?.Value is not null
                && DateTimeOffset.TryParse(setting.Value, out var lastImport)
                && DateTimeOffset.UtcNow - lastImport < StaleAfter
                && hasModuleSpecs)
            {
                _logger.LogInformation("Ship reference data imported {Age} ago, skipping (refreshes every 7 days)", DateTimeOffset.UtcNow - lastImport);
                return;
            }
        }

        try
        {
            await ImportAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ship reference data import failed - the fleet view will just show journal-derived data without extra specs");
        }
    }

    private async Task ImportAsync(CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(ReferenceDataImporter));

        _logger.LogInformation("Importing ship reference data from FDevIDs + coriolis-data");

        var symbolToDisplayName = await FetchShipyardCsvAsync(http, ct);
        var coriolisSpecsByNormalizedName = await FetchCoriolisSpecsAsync(http, ct);

        var specs = new List<ShipStaticSpec>();
        foreach (var (symbol, displayName) in symbolToDisplayName)
        {
            if (coriolisSpecsByNormalizedName.TryGetValue(Normalize(displayName), out var coriolisSpec))
            {
                specs.Add(new ShipStaticSpec
                {
                    Symbol = symbol,
                    DisplayName = displayName,
                    Manufacturer = coriolisSpec.Manufacturer,
                    SizeClass = coriolisSpec.Class,
                    HullMass = coriolisSpec.HullMass,
                    HullCost = coriolisSpec.HullCost,
                    BaseShieldStrength = coriolisSpec.BaseShieldStrength,
                    BaseArmour = coriolisSpec.BaseArmour,
                    CrewSeats = coriolisSpec.Crew,
                    LastUpdatedUtc = DateTime.UtcNow,
                });
            }
        }

        _logger.LogInformation("Matched {Matched}/{Total} ships against coriolis-data", specs.Count, symbolToDisplayName.Count);

        var moduleSpecs = await FetchOutfittingCsvAsync(http, ct);
        _logger.LogInformation("Imported {Count} module specs from FDevIDs outfitting.csv", moduleSpecs.Count);

        await _writeQueue.EnqueueAsync(async (db, innerCt) =>
        {
            var existing = await db.ShipStaticSpecs.ToListAsync(innerCt);
            db.ShipStaticSpecs.RemoveRange(existing);
            db.ShipStaticSpecs.AddRange(specs);

            var existingModules = await db.ModuleStaticSpecs.ToListAsync(innerCt);
            db.ModuleStaticSpecs.RemoveRange(existingModules);
            db.ModuleStaticSpecs.AddRange(moduleSpecs);

            var setting = await db.AppSettings.FindAsync([LastImportSettingKey], innerCt);
            if (setting is null)
            {
                db.AppSettings.Add(new AppSetting { Key = LastImportSettingKey, Value = DateTimeOffset.UtcNow.ToString("O") });
            }
            else
            {
                setting.Value = DateTimeOffset.UtcNow.ToString("O");
            }

            await db.SaveChangesAsync(innerCt);
        });
    }

    private static async Task<List<ModuleStaticSpec>> FetchOutfittingCsvAsync(HttpClient http, CancellationToken ct)
    {
        // id,symbol,category,name,mount,guidance,ship,class,rating,entitlement
        var csv = await http.GetStringAsync(OutfittingCsvUrl, ct);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var bySymbol = new Dictionary<string, ModuleStaticSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 4 || parts[1].Length == 0 || parts[3].Length == 0)
            {
                continue;
            }

            // Ship-specific bulkheads repeat the same symbol pattern per ship
            // (e.g. "SideWinder_Armour_Grade1", "Eagle_Armour_Grade1") - last
            // one in wins, which is fine since this is purely for display
            // names and duplicates across ships share the same name anyway.
            bySymbol[parts[1]] = new ModuleStaticSpec
            {
                Symbol = parts[1],
                Category = parts[2].Length > 0 ? parts[2] : null,
                DisplayName = parts[3],
                Mount = parts.Length > 4 && parts[4].Length > 0 ? parts[4] : null,
                Guidance = parts.Length > 5 && parts[5].Length > 0 ? parts[5] : null,
                Class = parts.Length > 7 && int.TryParse(parts[7], out var cls) ? cls : null,
                Rating = parts.Length > 8 && parts[8].Length > 0 ? parts[8] : null,
                LastUpdatedUtc = DateTime.UtcNow,
            };
        }

        return bySymbol.Values.ToList();
    }

    private static async Task<List<(string Symbol, string DisplayName)>> FetchShipyardCsvAsync(HttpClient http, CancellationToken ct)
    {
        var csv = await http.GetStringAsync(ShipyardCsvUrl, ct);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var results = new List<(string, string)>();
        foreach (var line in lines.Skip(1)) // skip header: id,symbol,name,entitlement
        {
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                results.Add((parts[1], parts[2]));
            }
        }

        return results;
    }

    private sealed record CoriolisSpec(string? Manufacturer, int Class, double HullMass, long HullCost, int BaseShieldStrength, int BaseArmour, int Crew);

    private async Task<Dictionary<string, CoriolisSpec>> FetchCoriolisSpecsAsync(HttpClient http, CancellationToken ct)
    {
        var listingJson = await http.GetStringAsync(CoriolisShipListUrl, ct);
        using var listingDoc = JsonDocument.Parse(listingJson);

        var fileNames = listingDoc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .Where(name => name is not null && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .ToList();

        var results = new Dictionary<string, CoriolisSpec>();
        using var gate = new SemaphoreSlim(5);

        var tasks = fileNames.Select(async fileName =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var url = string.Format(CoriolisShipRawUrlTemplate, fileName);
                var json = await http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.EnumerateObject().First().Value;
                var props = root.GetProperty("properties");

                var name = props.GetProperty("name").GetString();
                if (name is null)
                {
                    return;
                }

                var spec = new CoriolisSpec(
                    Manufacturer: props.TryGetProperty("manufacturer", out var m) ? m.GetString() : null,
                    Class: props.TryGetProperty("class", out var c) ? c.GetInt32() : 0,
                    HullMass: props.TryGetProperty("hullMass", out var hm) ? hm.GetDouble() : 0,
                    HullCost: props.TryGetProperty("hullCost", out var hc) ? hc.GetInt64() : 0,
                    BaseShieldStrength: props.TryGetProperty("baseShieldStrength", out var bs) ? bs.GetInt32() : 0,
                    BaseArmour: props.TryGetProperty("baseArmour", out var ba) ? ba.GetInt32() : 0,
                    Crew: props.TryGetProperty("crew", out var cr) ? cr.GetInt32() : 1);

                lock (results)
                {
                    results[Normalize(name)] = spec;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch/parse coriolis ship file {FileName}", fileName);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private static string Normalize(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
