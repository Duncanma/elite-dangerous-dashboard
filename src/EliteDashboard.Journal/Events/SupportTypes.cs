using System.Text.Json.Serialization;

namespace EliteDashboard.Journal.Events;

/// <summary>
/// Nested block on the Statistics event. Frontier uses underscored field
/// names here (unlike most of the journal), hence the explicit mappings.
/// </summary>
public sealed record BankAccountStats
{
    [JsonPropertyName("Current_Wealth")]
    public long CurrentWealth { get; init; }

    [JsonPropertyName("Spent_On_Ships")]
    public long SpentOnShips { get; init; }

    [JsonPropertyName("Spent_On_Outfitting")]
    public long SpentOnOutfitting { get; init; }

    [JsonPropertyName("Spent_On_Repairs")]
    public long SpentOnRepairs { get; init; }

    [JsonPropertyName("Spent_On_Fuel")]
    public long SpentOnFuel { get; init; }

    [JsonPropertyName("Spent_On_Ammo_Consumables")]
    public long SpentOnAmmoConsumables { get; init; }

    [JsonPropertyName("Insurance_Claims")]
    public long InsuranceClaims { get; init; }
}

/// <summary>
/// One entry in StoredShips' ShipsHere/ShipsRemote arrays. In-transit ships
/// omit most fields (only ShipID/ShipType/Value/Hot/InTransit are present).
/// </summary>
public sealed record StoredShipEntry
{
    public int ShipID { get; init; }
    public string? ShipType { get; init; }

    [JsonPropertyName("ShipType_Localised")]
    public string? ShipTypeLocalised { get; init; }

    public string? Name { get; init; }
    public string? StarSystem { get; init; }
    public bool InTransit { get; init; }
}

/// <summary>Engineering block on a Loadout module, present only if the module has been modified.</summary>
public sealed record ModuleEngineering
{
    public string? BlueprintName { get; init; }
    public int Level { get; init; }
    public double Quality { get; init; }

    [JsonPropertyName("ExperimentalEffect_Localised")]
    public string? ExperimentalEffectLocalised { get; init; }
}

/// <summary>One entry in Loadout's Modules array.</summary>
public sealed record LoadoutModule
{
    public string? Slot { get; init; }
    public string? Item { get; init; }
    public bool On { get; init; } = true;
    public int Priority { get; init; }
    public double Health { get; init; } = 1.0;
    public ModuleEngineering? Engineering { get; init; }
}

/// <summary>FuelCapacity block on Loadout - main tank size in tonnes. The reserve tank is an internal HUD detail, deliberately not modelled.</summary>
public sealed record FuelCapacityInfo
{
    public double Main { get; init; }
}

/// <summary>LandingPads block on Docked - real per-station pad counts, more precise than inferring from StationType.</summary>
public sealed record LandingPadCounts
{
    public int Small { get; init; }
    public int Medium { get; init; }
    public int Large { get; init; }
}

/// <summary>One entry in ColonisationConstructionDepot's ResourcesRequired array.</summary>
public sealed record ColonisationResourceEntry
{
    public string? Name { get; init; }

    [JsonPropertyName("Name_Localised")]
    public string? NameLocalised { get; init; }

    public long RequiredAmount { get; init; }
    public long ProvidedAmount { get; init; }
    public long Payment { get; init; }
}

/// <summary>One entry in ColonisationContribution's Contributions array (cargo delivered).</summary>
public sealed record ColonisationContributionEntry
{
    public string? Name { get; init; }

    [JsonPropertyName("Name_Localised")]
    public string? NameLocalised { get; init; }

    public long Amount { get; init; }
}
