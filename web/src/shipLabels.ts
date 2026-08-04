import type { ShipSummary } from "./api";

export function formatCredits(credits: number): string {
  return new Intl.NumberFormat().format(credits) + " CR";
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
