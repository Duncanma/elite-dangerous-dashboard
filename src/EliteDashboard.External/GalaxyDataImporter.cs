using System.Globalization;
using System.Text;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>
/// The trade route engine and colonisation shipping planner both need real
/// galactic coordinates for systems the commander has never personally
/// visited (and, for pad-size inference, real per-station data for stations
/// they've never docked at). Our own journal only gives us either - EDSM's
/// per-system live lookup (<see cref="EdsmClient"/>/<see
/// cref="SystemCoordinateResolver"/>) fills gaps one system at a time as
/// routes are searched, but that's a slow drip against a galaxy with
/// millions of systems. This importer instead pulls two static, whole-galaxy
/// CSV dumps from elite.tromador.com - a community-run continuation of the
/// old EDDB.io data feed, itself sourced from EDDN (the same firehose <see
/// cref="EliteDashboard.Trading.EddnListener"/> already listens to) - so
/// coverage jumps from "systems this commander has visited" to "every system
/// any commander has ever visited," in one weekly batch:
///
///   System.csv  (~14MB)  - system_id, name, x/y/z coordinates
///   Station.csv (~110MB) - station_id, name, system_id, max_pad_size (S/M/L), planetary (Y/N)
///
/// Two things make this strictly enrichment, never a source of truth:
///
/// 1. StarSystem coordinates are only ever filled in when
///    <see cref="StarSystem.HasCoordinates"/> is still false - the
///    commander's own FSDJump history (and any prior EDSM resolution) is
///    always left alone, since it's first-hand and this is second-hand.
///
/// 2. Station.csv's own station_id is EDDB's internal numbering, with no
///    relationship whatsoever to Frontier's MarketId (this app's only
///    canonical station key, used everywhere including resolving
///    Colonisation sites). So station rows can't be joined by ID, and for
///    the same reason this importer can never CREATE a Station row - it has
///    no MarketId to create one with. Instead, rows are matched by
///    (system name, station name) against stations this app already has
///    (upserted elsewhere, by EDDN or the commander's own Docked events),
///    and only ever write to the two dedicated
///    <see cref="Station.CommunityMaxPadSizeClass"/>/<see
///    cref="Station.CommunityPlanetary"/> fields - never the real
///    SmallPads/MediumPads/LargePads/StationType fields sourced from actual
///    dock events, which stay strictly more authoritative (see
///    StationCapabilities' tiered inference).
///
/// Systems must be imported before stations in the same pass: resolving a
/// station's system name requires a system_id -&gt; name lookup, and the only
/// place that mapping exists is System.csv itself.
///
/// Both files use a MySQL-dump style of CSV that isn't RFC4180: string
/// fields are wrapped in single quotes (not double quotes), with a literal
/// embedded apostrophe doubled (<c>'Moxon''s Mojo'</c> -&gt; <c>Moxon's
/// Mojo</c>). A standard CSV parser would misparse this, hence the small
/// hand-rolled <see cref="ParseLine"/> below.
/// </summary>
public sealed class GalaxyDataImporter
{
    private const string SystemCsvUrl = "https://elite.tromador.com/files/System.csv";
    private const string StationCsvUrl = "https://elite.tromador.com/files/Station.csv";
    private const string LastImportSettingKey = "GalaxyData.LastImportUtc";
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;
    private readonly DbWriteQueue _writeQueue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GalaxyDataImporter> _logger;

    public GalaxyDataImporter(
        IDbContextFactory<DashboardDbContext> contextFactory, DbWriteQueue writeQueue,
        IHttpClientFactory httpClientFactory, ILogger<GalaxyDataImporter> logger)
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
            if (setting?.Value is not null
                && DateTimeOffset.TryParse(setting.Value, out var lastImport)
                && DateTimeOffset.UtcNow - lastImport < StaleAfter)
            {
                _logger.LogInformation("Galaxy reference data imported {Age} ago, skipping (refreshes every 7 days)", DateTimeOffset.UtcNow - lastImport);
                return;
            }
        }

        try
        {
            await ImportAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Galaxy reference data import failed - trade routes and pad-size inference will just have less coverage than they could until the next attempt");
        }
    }

    private async Task ImportAsync(CancellationToken ct)
    {
        var http = _httpClientFactory.CreateClient(nameof(GalaxyDataImporter));

        _logger.LogInformation("Importing galaxy reference data from elite.tromador.com");

        Dictionary<string, (long Id, bool HasCoordinates)> systemsByName;
        Dictionary<string, long> stationIdsByKey;
        await using (var db = await _contextFactory.CreateDbContextAsync(ct))
        {
            systemsByName = new Dictionary<string, (long, bool)>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in await db.StarSystems.Select(s => new { s.Id, s.Name, s.HasCoordinates }).ToListAsync(ct))
            {
                systemsByName[row.Name] = (row.Id, row.HasCoordinates);
            }

            stationIdsByKey = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in await db.Stations
                .Where(st => st.StarSystem != null)
                .Select(st => new { st.Id, StationName = st.Name, SystemName = st.StarSystem!.Name })
                .ToListAsync(ct))
            {
                stationIdsByKey[StationKey(row.SystemName, row.StationName)] = row.Id;
            }
        }

        _logger.LogInformation("Matching against {SystemCount} known systems, {StationCount} known stations", systemsByName.Count, stationIdsByKey.Count);

        // Pass 1: System.csv. Builds the system_id -> name lookup Station.csv
        // needs, and enriches any known system still missing coordinates.
        var systemIdToName = new Dictionary<long, string>();
        var systemUpdates = new List<(long Id, double X, double Y, double Z)>();

        await using (var stream = await http.GetStreamAsync(SystemCsvUrl, ct))
        using (var reader = new StreamReader(stream))
        {
            await reader.ReadLineAsync(ct); // header
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                var fields = ParseLine(line);
                if (fields.Count < 6 || !long.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var systemId))
                {
                    continue;
                }

                var name = fields[1];
                systemIdToName[systemId] = name;

                if (systemsByName.TryGetValue(name, out var known) && !known.HasCoordinates
                    && double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    && double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                    && double.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    systemUpdates.Add((known.Id, x, y, z));
                }
            }
        }

        _logger.LogInformation("Parsed {Count} systems from System.csv, enriching {UpdateCount} with coordinates", systemIdToName.Count, systemUpdates.Count);

        // Pass 2: Station.csv. Resolves each row's system name via the
        // lookup above, then matches (system, station) name against our own
        // known stations.
        var stationUpdates = new List<(long Id, int? PadSizeClass, bool? Planetary)>();

        await using (var stream = await http.GetStreamAsync(StationCsvUrl, ct))
        using (var reader = new StreamReader(stream))
        {
            await reader.ReadLineAsync(ct); // header
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                var fields = ParseLine(line);
                if (fields.Count < 14 || !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var systemId))
                {
                    continue;
                }

                if (!systemIdToName.TryGetValue(systemId, out var systemName))
                {
                    continue;
                }

                var stationName = fields[1];
                if (!stationIdsByKey.TryGetValue(StationKey(systemName, stationName), out var stationDbId))
                {
                    continue;
                }

                int? padSizeClass = fields[6] switch
                {
                    "S" => 1,
                    "M" => 2,
                    "L" => 3,
                    _ => null,
                };

                bool? planetary = fields[13] switch
                {
                    "Y" => true,
                    "N" => false,
                    _ => null,
                };

                if (padSizeClass is not null || planetary is not null)
                {
                    stationUpdates.Add((stationDbId, padSizeClass, planetary));
                }
            }
        }

        _logger.LogInformation("Parsed Station.csv, enriching {UpdateCount} known stations with community pad-size/planetary data", stationUpdates.Count);

        await _writeQueue.EnqueueAsync(async (db, innerCt) =>
        {
            foreach (var (id, x, y, z) in systemUpdates)
            {
                var row = await db.StarSystems.FindAsync([id], innerCt);
                if (row is null || row.HasCoordinates)
                {
                    continue; // raced with a real FSDJump/EDSM resolution since the read above - leave it alone
                }

                row.X = x;
                row.Y = y;
                row.Z = z;
                row.HasCoordinates = true;
            }

            foreach (var (id, padSizeClass, planetary) in stationUpdates)
            {
                var row = await db.Stations.FindAsync([id], innerCt);
                if (row is null)
                {
                    continue;
                }

                if (padSizeClass is not null)
                {
                    row.CommunityMaxPadSizeClass = padSizeClass;
                }

                if (planetary is not null)
                {
                    row.CommunityPlanetary = planetary;
                }
            }

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

        _logger.LogInformation(
            "Galaxy reference data import complete: {SystemsEnriched} systems, {StationsEnriched} stations enriched",
            systemUpdates.Count, stationUpdates.Count);
    }

    /// <summary>
    /// Station names aren't unique galaxy-wide (plenty of "Jameson Memorial"
    /// duplicates don't exist, but generic names like "Trading Post" surely
    /// do), so the real match key is always the (system, station) pair.
    /// U+241F is a control picture character that will never appear in a
    /// real system/station name - safe as a join separator.
    /// </summary>
    private static string StationKey(string systemName, string stationName) => $"{systemName}␟{stationName}";

    /// <summary>
    /// Parses one line of Tromador's non-standard CSV: fields separated by
    /// commas, string values wrapped in single quotes with an embedded
    /// apostrophe doubled (<c>''</c>) rather than backslash-escaped. Numeric
    /// fields (ids, coordinates) are bare, unquoted.
    /// </summary>
    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var i = 0;
        var n = line.Length;

        while (i < n)
        {
            if (line[i] == '\'')
            {
                var sb = new StringBuilder();
                i++;
                while (i < n)
                {
                    if (line[i] == '\'')
                    {
                        if (i + 1 < n && line[i + 1] == '\'')
                        {
                            sb.Append('\'');
                            i += 2;
                            continue;
                        }

                        i++; // closing quote
                        break;
                    }

                    sb.Append(line[i]);
                    i++;
                }

                fields.Add(sb.ToString());
            }
            else
            {
                var start = i;
                while (i < n && line[i] != ',')
                {
                    i++;
                }

                fields.Add(line[start..i]);
            }

            if (i < n && line[i] == ',')
            {
                i++;
            }
        }

        return fields;
    }
}
