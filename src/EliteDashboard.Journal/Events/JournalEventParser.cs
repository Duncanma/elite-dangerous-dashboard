using System.Text.Json;

namespace EliteDashboard.Journal.Events;

/// <summary>
/// Parses a single journal JSONL line into a typed JournalEvent.
/// Only a handful of event types are modeled so far (Phase 1 spike); everything
/// else becomes an UnknownEvent rather than throwing, since the journal schema
/// is large and still evolving (e.g. Colonisation, Powerplay 2.0).
/// </summary>
public static class JournalEventParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static JournalEvent Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var eventName = root.TryGetProperty("event", out var evProp) ? evProp.GetString() ?? "Unknown" : "Unknown";

        JournalEvent? parsed = eventName switch
        {
            "Fileheader" => JsonSerializer.Deserialize<FileheaderEvent>(json, Options),
            "Commander" => JsonSerializer.Deserialize<CommanderEvent>(json, Options),
            "LoadGame" => JsonSerializer.Deserialize<LoadGameEvent>(json, Options),
            "FSDJump" => JsonSerializer.Deserialize<FsdJumpEvent>(json, Options),
            "Docked" => JsonSerializer.Deserialize<DockedEvent>(json, Options),
            "Undocked" => JsonSerializer.Deserialize<UndockedEvent>(json, Options),
            "Rank" => JsonSerializer.Deserialize<RankEvent>(json, Options),
            "Progress" => JsonSerializer.Deserialize<ProgressEvent>(json, Options),
            "Statistics" => JsonSerializer.Deserialize<StatisticsEvent>(json, Options),
            "Loadout" => JsonSerializer.Deserialize<LoadoutEvent>(json, Options),
            "StoredShips" => JsonSerializer.Deserialize<StoredShipsEvent>(json, Options),
            "MarketBuy" => JsonSerializer.Deserialize<MarketBuyEvent>(json, Options),
            "MarketSell" => JsonSerializer.Deserialize<MarketSellEvent>(json, Options),
            "RedeemVoucher" => JsonSerializer.Deserialize<RedeemVoucherEvent>(json, Options),
            "MissionCompleted" => JsonSerializer.Deserialize<MissionCompletedEvent>(json, Options),
            "RefuelAll" => JsonSerializer.Deserialize<RefuelAllEvent>(json, Options),
            "Repair" => JsonSerializer.Deserialize<RepairEvent>(json, Options),
            "RepairAll" => JsonSerializer.Deserialize<RepairAllEvent>(json, Options),
            "BuyAmmo" => JsonSerializer.Deserialize<BuyAmmoEvent>(json, Options),
            "BuyDrones" => JsonSerializer.Deserialize<BuyDronesEvent>(json, Options),
            "Resurrect" => JsonSerializer.Deserialize<ResurrectEvent>(json, Options),
            "SearchAndRescue" => JsonSerializer.Deserialize<SearchAndRescueEvent>(json, Options),
            "NpcCrewPaidWage" => JsonSerializer.Deserialize<NpcCrewPaidWageEvent>(json, Options),
            "Location" => JsonSerializer.Deserialize<LocationEvent>(json, Options),
            "ShipyardBuy" => JsonSerializer.Deserialize<ShipyardBuyEvent>(json, Options),
            "ShipyardSell" => JsonSerializer.Deserialize<ShipyardSellEvent>(json, Options),
            "ColonisationConstructionDepot" => JsonSerializer.Deserialize<ColonisationConstructionDepotEvent>(json, Options),
            "ColonisationContribution" => JsonSerializer.Deserialize<ColonisationContributionEvent>(json, Options),
            "ColonisationSystemClaim" => JsonSerializer.Deserialize<ColonisationSystemClaimEvent>(json, Options),
            "Powerplay" => JsonSerializer.Deserialize<PowerplayEvent>(json, Options),
            "PowerplayRank" => JsonSerializer.Deserialize<PowerplayRankEvent>(json, Options),
            "PowerplayMerits" => JsonSerializer.Deserialize<PowerplayMeritsEvent>(json, Options),
            _ => null,
        };

        return parsed ?? new UnknownEvent { Event = eventName, Timestamp = TryGetTimestamp(root) };
    }

    private static DateTimeOffset TryGetTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestamp", out var ts) && ts.TryGetDateTimeOffset(out var value))
        {
            return value;
        }

        return DateTimeOffset.UtcNow;
    }
}
