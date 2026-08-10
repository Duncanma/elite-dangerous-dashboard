namespace EliteDashboard.Journal.Events;

/// <summary>
/// Base envelope for a parsed journal event. Every concrete journal event type
/// gets its own record; anything not yet modeled becomes an <see cref="UnknownEvent"/>
/// so the raw line is never silently dropped by the parser.
/// </summary>
public abstract record JournalEvent
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Event { get; init; }
}

public sealed record FileheaderEvent : JournalEvent
{
    public int Part { get; init; }
    public string? GameVersion { get; init; }
    public bool Odyssey { get; init; }
}

public sealed record CommanderEvent : JournalEvent
{
    public string? FID { get; init; }
    public string? Name { get; init; }
}

public sealed record LoadGameEvent : JournalEvent
{
    public string? FID { get; init; }
    public string? Commander { get; init; }
    public string? Ship { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("Ship_Localised")]
    public string? ShipLocalised { get; init; }

    public string? ShipName { get; init; }
    public long Credits { get; init; }
    public bool Odyssey { get; init; }
    public bool Horizons { get; init; }
}

public sealed record FsdJumpEvent : JournalEvent
{
    public string? StarSystem { get; init; }
    public double[]? StarPos { get; init; }
}

public sealed record DockedEvent : JournalEvent
{
    public long MarketID { get; init; }
    public string? StationName { get; init; }
    public string? StationType { get; init; }
    public string? StarSystem { get; init; }
    public LandingPadCounts? LandingPads { get; init; }
}

public sealed record UndockedEvent : JournalEvent
{
    public string? StationName { get; init; }
}

public sealed record RankEvent : JournalEvent
{
    public int Combat { get; init; }
    public int Trade { get; init; }
    public int Explore { get; init; }
    public int CQC { get; init; }
    public int Federation { get; init; }
    public int Empire { get; init; }
}

/// <summary>Percent progress (0-99) toward the next tier within each of Rank's current tiers.</summary>
public sealed record ProgressEvent : JournalEvent
{
    public int Combat { get; init; }
    public int Trade { get; init; }
    public int Explore { get; init; }
    public int CQC { get; init; }
    public int Federation { get; init; }
    public int Empire { get; init; }
}

public sealed record StatisticsEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("Bank_Account")]
    public BankAccountStats? BankAccount { get; init; }
}

public sealed record LoadoutEvent : JournalEvent
{
    public int ShipID { get; init; }
    public string? Ship { get; init; }
    public string? ShipName { get; init; }
    public string? ShipIdent { get; init; }
    public double CargoCapacity { get; init; }
    public double MaxJumpRange { get; init; }
    public FuelCapacityInfo? FuelCapacity { get; init; }
    public double UnladenMass { get; init; }
    public long Rebuy { get; init; }
    public long HullValue { get; init; }
    public long ModulesValue { get; init; }
    public List<LoadoutModule> Modules { get; init; } = [];
}

public sealed record StoredShipsEvent : JournalEvent
{
    public string? StationName { get; init; }
    public string? StarSystem { get; init; }
    public List<StoredShipEntry> ShipsHere { get; init; } = [];
    public List<StoredShipEntry> ShipsRemote { get; init; } = [];
}

public sealed record MarketBuyEvent : JournalEvent
{
    public long MarketID { get; init; }
    public string? Type { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("Type_Localised")]
    public string? TypeLocalised { get; init; }

    public int Count { get; init; }
    public long TotalCost { get; init; }
}

public sealed record MarketSellEvent : JournalEvent
{
    public long MarketID { get; init; }
    public string? Type { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("Type_Localised")]
    public string? TypeLocalised { get; init; }

    public int Count { get; init; }
    public long TotalSale { get; init; }
}

public sealed record RedeemVoucherEvent : JournalEvent
{
    public string? Type { get; init; }
    public long Amount { get; init; }
}

public sealed record MissionCompletedEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("LocalisedName")]
    public string? LocalisedName { get; init; }

    public long Reward { get; init; }
}

public sealed record RefuelAllEvent : JournalEvent
{
    public long Cost { get; init; }
}

public sealed record RepairEvent : JournalEvent
{
    public long Cost { get; init; }
}

/// <summary>The "repair all modules" action - a separate event from Repair (which is a single module).</summary>
public sealed record RepairAllEvent : JournalEvent
{
    public long Cost { get; init; }
}

public sealed record BuyAmmoEvent : JournalEvent
{
    public long Cost { get; init; }
}

public sealed record BuyDronesEvent : JournalEvent
{
    public int Count { get; init; }
    public long TotalCost { get; init; }
}

public sealed record SellDronesEvent : JournalEvent
{
    public int Count { get; init; }
    public long TotalSale { get; init; }
}

/// <summary>Fired when choosing a respawn option after Died. Option is "rebuy" or "free"; only "rebuy" carries a real Cost.</summary>
public sealed record ResurrectEvent : JournalEvent
{
    public string? Option { get; init; }
    public long Cost { get; init; }
    public bool Bankrupt { get; init; }
}

/// <summary>Outfitting purchase. May include a simultaneous trade-in of the previous module (SellPrice).</summary>
public sealed record ModuleBuyEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("BuyItem_Localised")]
    public string? BuyItemLocalised { get; init; }

    public long BuyPrice { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("SellItem_Localised")]
    public string? SellItemLocalised { get; init; }

    public long SellPrice { get; init; }
}

/// <summary>Buy a module and put it in storage rather than fitting it.</summary>
public sealed record ModuleBuyAndStoreEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("BuyItem_Localised")]
    public string? BuyItemLocalised { get; init; }

    public long BuyPrice { get; init; }
}

public sealed record ModuleSellEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("SellItem_Localised")]
    public string? SellItemLocalised { get; init; }

    public long SellPrice { get; init; }
}

public sealed record RestockVehicleEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("Type_Localised")]
    public string? TypeLocalised { get; init; }

    public string? Type { get; init; }
    public long Cost { get; init; }
    public int Count { get; init; }
}

public sealed record CrewHireEvent : JournalEvent
{
    public string? Name { get; init; }
    public long Cost { get; init; }
}

public sealed record RefuelPartialEvent : JournalEvent
{
    public long Cost { get; init; }
}

public sealed record BuyExplorationDataEvent : JournalEvent
{
    public string? System { get; init; }
    public long Cost { get; init; }
}

/// <summary>Fired when handing in salvage (escape pods, black boxes, etc.) at a station's search-and-rescue contact.</summary>
public sealed record SearchAndRescueEvent : JournalEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("Name_Localised")]
    public string? NameLocalised { get; init; }

    public long Reward { get; init; }
}

public sealed record NpcCrewPaidWageEvent : JournalEvent
{
    public string? NpcCrewName { get; init; }
    public long Amount { get; init; }
}

/// <summary>
/// Fired instead of FSDJump/Docked when a session starts already in a system
/// (e.g. right after login, or after a Resurrect respawn) - the only other
/// source of StarPos/current-station info besides those two events.
/// </summary>
public sealed record LocationEvent : JournalEvent
{
    public string? StarSystem { get; init; }
    public double[]? StarPos { get; init; }
    public bool Docked { get; init; }
    public string? StationName { get; init; }
    public string? StationType { get; init; }
    public long MarketID { get; init; }
}

public sealed record ShipyardBuyEvent : JournalEvent
{
    public string? ShipType { get; init; }
    public long ShipPrice { get; init; }
}

public sealed record ShipyardSellEvent : JournalEvent
{
    public string? ShipType { get; init; }
    public long SellPrice { get; init; }
}

public sealed record ColonisationConstructionDepotEvent : JournalEvent
{
    public long MarketID { get; init; }
    public double ConstructionProgress { get; init; }
    public bool ConstructionComplete { get; init; }
    public bool ConstructionFailed { get; init; }
    public List<ColonisationResourceEntry> ResourcesRequired { get; init; } = [];
}

public sealed record ColonisationSystemClaimEvent : JournalEvent
{
    public string? StarSystem { get; init; }
    public long SystemAddress { get; init; }
}

/// <summary>
/// Fired only when cargo is actually delivered to a construction site -
/// unlike ColonisationConstructionDepot, which is a snapshot the game sends
/// for any site with the contribution service you've merely docked at, even
/// with zero involvement. Used to distinguish "sites I'm building" from
/// "sites I once passed through".
/// </summary>
public sealed record ColonisationContributionEvent : JournalEvent
{
    public long MarketID { get; init; }
    public List<ColonisationContributionEntry> Contributions { get; init; } = [];
}

/// <summary>Startup-only event, present only if the commander is currently pledged.</summary>
public sealed record PowerplayEvent : JournalEvent
{
    public string? Power { get; init; }
    public int Rank { get; init; }
    public long Merits { get; init; }
    public long TimePledged { get; init; }
}

public sealed record PowerplayRankEvent : JournalEvent
{
    public string? Power { get; init; }
    public int Rank { get; init; }
}

public sealed record PowerplayMeritsEvent : JournalEvent
{
    public string? Power { get; init; }
    public long MeritsGained { get; init; }
    public long TotalMerits { get; init; }
}

/// <summary>
/// Fallback for any event type not yet modeled. The raw line is preserved
/// upstream regardless (see JournalLine) so nothing is lost even for unknowns.
/// </summary>
public sealed record UnknownEvent : JournalEvent;
