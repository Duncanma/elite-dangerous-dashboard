import type { ShipSummary } from "./api";

export function formatCredits(credits: number): string {
  return new Intl.NumberFormat().format(credits) + " CR";
}

export function locationLabel(ship: ShipSummary): string {
  switch (ship.location) {
    case "Current":
      return "Currently flying";
    case "Here":
      return "Docked here";
    case "InTransit":
      return "In transit";
    case "Remote":
      return ship.starSystem ? `Stored at ${ship.starSystem}` : "Stored remotely";
  }
}
