import type { ShipSummary } from "./api";

// Large credit amounts are common (fleet/hull values, net worth) and a full
// "887,234,156 CR" is hard to scan at a glance - shorten to "887.2 M"/"3 K"
// past 1,000/1,000,000, same convention as EconomicsPage's chart-axis
// formatter. `signed` prefixes a "+" for positive amounts (negative amounts
// already get their own "-" from the number formatting) - used by the
// Economics page's income/expense/net figures, where the sign itself is
// meaningful, not just implied by a green/red CSS class.
export function formatCredits(credits: number, options?: { signed?: boolean }): string {
  const sign = options?.signed && credits > 0 ? "+" : "";
  const abs = Math.abs(credits);

  if (abs >= 1_000_000) {
    return sign + trimCompact(credits / 1_000_000) + " M";
  }
  if (abs >= 1_000) {
    return sign + trimCompact(credits / 1_000) + " K";
  }
  return sign + new Intl.NumberFormat().format(credits) + " CR";
}

// One decimal place, but drop a trailing ".0" (2_000_000 -> "2", not "2.0").
function trimCompact(value: number): string {
  return value.toFixed(1).replace(/\.0$/, "");
}

// Just the real location, system and station where known - no interpretive
// wording ("docked"/"stored"/"currently flying"), since every non-flown ship
// is in the same underlying state (parked in a hangar somewhere).
//
// ship.starSystem/station is a snapshot from whenever the ship was last
// reconciled (a Loadout or StoredShips event) - fine for stored ships, but
// for the *currently flown* ship that can visibly lag the commander's real
// position (a jump/dock doesn't itself re-fire Loadout). DashboardState's own
// currentSystem/currentStation update on every FSDJump/Docked/Undocked, so
// pass them as liveLocation to keep the currently-flown ship's row always in
// sync with the rest of the page instead of showing a stale snapshot.
export function shipLocationText(ship: ShipSummary, liveLocation?: { system: string | null; station: string | null }): string {
  const useLive = ship.location === "Current" && liveLocation;
  const system = useLive ? liveLocation.system : ship.starSystem;
  const station = useLive ? liveLocation.station : ship.station;
  if (!system) return "Unknown location";
  return station ? `${system} — ${station}` : system;
}
