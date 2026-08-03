using System.Text.Json;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>
/// Periodically pushes a batch of the commander's own profile data to Inara.
/// Inara explicitly asks integrations not to spam per-event updates (their
/// docs suggest "session start, session end, or hourly intervals" for
/// credits specifically) - this runs on a fixed interval and always sends
/// everything as one batched request, well under their ~2 req/min guidance.
/// A no-op (not an error) whenever no API key is configured.
/// </summary>
public sealed class InaraSyncService : BackgroundService
{
    public const string ApiKeySettingKey = "Inara.ApiKey";
    public const string LastSyncStatusSettingKey = "Inara.LastSyncStatus";
    public const string LastSyncAtSettingKey = "Inara.LastSyncAtUtc";

    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(10);

    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;
    private readonly StateStore _stateStore;
    private readonly InaraClient _client;
    private readonly ILogger<InaraSyncService> _logger;

    public InaraSyncService(
        IDbContextFactory<DashboardDbContext> contextFactory, StateStore stateStore,
        InaraClient client, ILogger<InaraSyncService> logger)
    {
        _contextFactory = contextFactory;
        _stateStore = stateStore;
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SyncInterval);

        // Sync once shortly after startup too, not just after the first full interval.
        await TrySyncAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken ct)
    {
        try
        {
            await SyncAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inara sync failed");
            await RecordStatusAsync($"Error: {ex.Message}", ct);
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var apiKeySetting = await db.AppSettings.FindAsync([ApiKeySettingKey], ct);
        var apiKey = apiKeySetting?.Value;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var state = _stateStore.Current;
        if (state.Name is null)
        {
            return;
        }

        var events = new List<InaraEvent>();
        var now = DateTimeOffset.UtcNow;

        events.Add(new InaraEvent("setCommanderCredits", now, new { commanderCredits = state.Economy.Credits, commanderLoan = 0 }));

        var ranks = new List<object>
        {
            new { rankName = "combat", rankValue = state.Rank.Combat },
            new { rankName = "trade", rankValue = state.Rank.Trade },
            new { rankName = "explore", rankValue = state.Rank.Explore },
            new { rankName = "cqc", rankValue = state.Rank.CQC },
        };
        events.Add(new InaraEvent("setCommanderRankPilot", now, ranks));

        var standing = await db.PowerplayStandings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (standing is not null)
        {
            events.Add(new InaraEvent("setCommanderRankPower", now, new
            {
                powerName = standing.Power,
                rankValue = standing.Rank,
                meritsValue = standing.TotalMerits,
            }));
        }

        // Forward the game's own Statistics event verbatim - Inara's
        // setCommanderGameStatistics is documented to accept exactly the
        // journal's own Statistics payload shape, so no reshaping needed.
        var latestStatistics = await db.JournalEvents.AsNoTracking()
            .Where(e => e.EventType == "Statistics")
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefaultAsync(ct);
        if (latestStatistics is not null)
        {
            using var doc = JsonDocument.Parse(latestStatistics.RawJson);
            events.Add(new InaraEvent("setCommanderGameStatistics", now, doc.RootElement.Clone()));
        }

        var results = await _client.SendEventsAsync(apiKey, state.Name, events, ct);
        var failures = results.Count(r => r.EventStatus >= 400);

        await RecordStatusAsync(
            failures == 0 ? $"OK ({results.Count} events)" : $"{failures}/{results.Count} events failed",
            ct);

        _logger.LogInformation("Inara sync sent {Count} events, {Failures} failed", events.Count, failures);
    }

    private async Task RecordStatusAsync(string status, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        await UpsertSettingAsync(db, LastSyncStatusSettingKey, status, ct);
        await UpsertSettingAsync(db, LastSyncAtSettingKey, DateTimeOffset.UtcNow.ToString("O"), ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertSettingAsync(DashboardDbContext db, string key, string value, CancellationToken ct)
    {
        var setting = await db.AppSettings.FindAsync([key], ct);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
    }
}
