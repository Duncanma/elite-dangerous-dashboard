// The API always runs on a fixed port on the same host that served this page,
// whether that's localhost during dev or a LAN IP from another device.
export const apiBaseUrl = `${window.location.protocol}//${window.location.hostname}:5080`;

export interface RankStanding {
  combat: number;
  trade: number;
  explore: number;
  cqc: number;
  federation: number;
  empire: number;
  combatProgress: number;
  tradeProgress: number;
  exploreProgress: number;
  cqcProgress: number;
  federationProgress: number;
  empireProgress: number;
}

export interface EconomicSnapshot {
  credits: number;
  loan: number;
  currentWealth: number;
  spentOnShips: number;
  spentOnOutfitting: number;
  spentOnRepairs: number;
  spentOnFuel: number;
  spentOnAmmoConsumables: number;
  insuranceClaims: number;
}

export interface ShipStatusSnapshot {
  flags: number;
  cargoTons: number;
  fuelMain: number;
  legalState: string | null;
}

export type ShipLocationKind = "Current" | "Here" | "Remote" | "InTransit";

export interface ShipSummary {
  shipId: number;
  shipType: string | null;
  shipTypeDisplay: string | null;
  name: string | null;
  starSystem: string | null;
  station: string | null;
  location: ShipLocationKind;
}

export interface ShipModuleSummary {
  slot: string | null;
  item: string | null;
  on: boolean;
  priority: number;
  health: number;
  engineeringBlueprint: string | null;
  engineeringLevel: number;
  experimentalEffect: string | null;
}

/** The last Loadout snapshot persisted for a ship (GET /api/fleet/{shipId}/loadout). */
export interface ShipLoadoutDetail {
  shipId: number;
  shipType: string | null;
  seenAtUtc: string;
  cargoCapacity: number;
  maxJumpRange: number;
  fuelCapacityMain: number;
  unladenMass: number;
  rebuy: number;
  hullValue: number;
  modulesValue: number;
  modules: ShipModuleSummary[];
}

export interface DashboardState {
  fid: string | null;
  name: string | null;
  currentSystem: string | null;
  currentStation: string | null;
  docked: boolean;

  shipType: string | null;
  shipTypeDisplay: string | null;
  shipName: string | null;
  shipIdent: string | null;
  shipId: number | null;
  cargoCapacity: number;
  maxJumpRange: number;
  rebuy: number;
  hullValue: number;
  modulesValue: number;

  rank: RankStanding;
  economy: EconomicSnapshot;
  shipStatus: ShipStatusSnapshot;
  fleet: ShipSummary[];
  currentShipModules: ShipModuleSummary[];

  lastUpdatedUtc: string;
}

export type TransactionCategory =
  | "TradeBuy"
  | "TradeSell"
  | "BountyVoucher"
  | "BondVoucher"
  | "ExplorationVoucher"
  | "MissionReward"
  | "Repair"
  | "Refuel"
  | "ShipyardBuy"
  | "ShipyardSell"
  | "Rearm"
  | "Rebuy"
  | "SearchAndRescue"
  | "CrewWages"
  | "Other";

export interface Transaction {
  id: number;
  timestampUtc: string;
  category: TransactionCategory;
  amount: number;
  description: string | null;
}

export interface BalancePoint {
  timestampUtc: string;
  balance: number;
}

export interface CreditGoal {
  id: number;
  targetAmount: number;
  targetDate: string | null;
  createdUtc: string;
  note: string | null;
  isActive: boolean;
}

export interface ShipWishlistItem {
  id: number;
  shipType: string;
  displayName: string | null;
  targetLoadoutNotes: string | null;
  estimatedCost: number | null;
  priority: number;
  createdUtc: string;
  purchasedUtc: string | null;
}

export interface ShipStaticSpec {
  displayName: string;
  manufacturer: string | null;
  sizeClass: number;
  hullMass: number;
  hullCost: number;
  baseShieldStrength: number;
  baseArmour: number;
  crewSeats: number;
}

export interface ModuleStaticSpec {
  displayName: string;
  category: string | null;
  mount: string | null;
  class: number | null;
  rating: string | null;
}

export function shipImageUrl(shipType: string, display?: string | null, sizeClass?: number, shipName?: string | null): string {
  const params = new URLSearchParams();
  if (display) params.set("display", display);
  if (sizeClass) params.set("sizeClass", String(sizeClass));
  if (shipName) params.set("shipName", shipName);
  const query = params.toString();
  return `${apiBaseUrl}/api/ships/${encodeURIComponent(shipType)}/image${query ? `?${query}` : ""}`;
}

export function sizeClassLabel(sizeClass: number): string {
  switch (sizeClass) {
    case 1:
      return "Small";
    case 2:
      return "Medium";
    case 3:
      return "Large";
    default:
      return "Unknown";
  }
}

export interface EddnStatus {
  connected: boolean;
  lastMessageAtUtc: string | null;
  messagesReceived: number;
  commodityMessagesProcessed: number;
}

export interface TradeSearchCriteria {
  originStationId: number | null;
  originMarketId: string | null;
  originSystemName: string | null;
  cargoCapacity: number;
  credits: number;
  maxHops: number;
  shipJumpRangeLy: number;
  maxJumpsFromOrigin: number;
  maxJumpsBetweenStations: number;
  requiredShipSizeClass: number | null;
  includeUnknownPadSize: boolean;
  excludePlanetary: boolean;
  excludeFleetCarriers: boolean;
}

export interface TradeSystemCoverage {
  knownSystems: number;
  unresolvedSystems: number;
}

export interface TradeSearchDefaults {
  cargoCapacity: number;
  credits: number;
  shipJumpRangeLy: number;
  originSystemName: string | null;
  requiredShipSizeClass: number | null;
  maxHops: number;
  maxJumpsFromOrigin: number;
  maxJumpsBetweenStations: number;
  includeUnknownPadSize: boolean;
  excludePlanetary: boolean;
  excludeFleetCarriers: boolean;
}

export interface TradeRouteCandidate {
  originStationId: number;
  originStationName: string;
  originSystemName: string;
  originStationType: string | null;
  destinationStationId: number;
  destinationStationName: string;
  destinationSystemName: string;
  destinationStationType: string | null;
  commodityId: number;
  commodityName: string;
  buyPrice: number;
  sellPrice: number;
  quantity: number;
  totalProfit: number;
}

export type TradeRoutePlanStatus = "Draft" | "Active" | "Completed" | "Abandoned";

export interface TradeRouteHop {
  id: number;
  sequenceNumber: number;
  originStationName: string;
  originSystemName: string;
  originStationType: string | null;
  destinationStationName: string;
  destinationSystemName: string;
  destinationStationType: string | null;
  commodityName: string;
  plannedBuyPrice: number;
  plannedSellPrice: number;
  plannedQuantity: number;
  plannedProfit: number;
  boughtUtc: string | null;
  soldUtc: string | null;
}

export interface TradeRoutePlan {
  id: number;
  status: TradeRoutePlanStatus;
  createdUtc: string;
  startingCapital: number;
  totalHops: number;
  estimatedProfit: number;
  activatedUtc: string | null;
  completedUtc: string | null;
  hops: TradeRouteHop[];
}

export interface ColonisationResource {
  commodityName: string;
  commodityDisplayName: string | null;
  requiredAmount: number;
  providedAmount: number;
  payment: number;
}

export interface ColonisationSite {
  id: number;
  marketId: number;
  stationName: string;
  systemName: string;
  constructionProgress: number;
  constructionComplete: boolean;
  constructionFailed: boolean;
  firstSeenUtc: string;
  lastUpdatedUtc: string;
  resources: ColonisationResource[];
}

export interface ShippingPlanItem {
  commodityName: string;
  quantity: number;
}

export interface ShippingPlanStop {
  stationName: string;
  systemName: string;
  stationType: string | null;
  distanceLy: number;
  items: ShippingPlanItem[];
}

export interface ShippingPlanResult {
  success: boolean;
  message: string | null;
  stops: ShippingPlanStop[];
  unresolvedCommodities: string[];
}

export interface PowerplayStanding {
  power: string;
  rank: number;
  totalMerits: number;
  timePledgedSeconds: number;
  lastUpdatedUtc: string;
}

export interface PowerplayHistoryPoint {
  timestampUtc: string;
  power: string;
  totalMerits: number;
  meritsDelta: number;
}
