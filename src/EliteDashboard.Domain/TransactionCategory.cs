namespace EliteDashboard.Domain;

public enum TransactionCategory
{
    TradeBuy,
    TradeSell,
    BountyVoucher,
    BondVoucher,
    ExplorationVoucher,
    MissionReward,
    Repair,
    Refuel,
    ShipyardBuy,
    ShipyardSell,
    Rearm,
    Rebuy,
    SearchAndRescue,
    CrewWages,
    /// <summary>ModuleBuy / ModuleSell / ModuleBuyAndStore (net of any simultaneous trade-in).</summary>
    Outfitting,
    /// <summary>RestockVehicle — SLF / SRV restock at a station.</summary>
    VehicleRestock,
    /// <summary>CrewHire — upfront cost to hire an NPC crew member (distinct from ongoing CrewWages).</summary>
    CrewHire,
    Other,
}
