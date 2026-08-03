import { useEffect, useState } from "react";
import { apiBaseUrl, type ShipStaticSpec } from "./api";

/**
 * Frontier's own journal is inconsistent about ship symbol casing between
 * event types - e.g. LoadGame/Loadout write "explorer_nx" (lowercase) for
 * the ship you're currently flying, while StoredShips and FDevIDs both use
 * "Explorer_NX". Keys are normalized to lowercase here so lookups don't
 * silently miss real matches over a casing difference alone.
 */
export function useShipSpecs() {
  const [specs, setSpecs] = useState<Record<string, ShipStaticSpec>>({});

  useEffect(() => {
    fetch(`${apiBaseUrl}/api/fleet/specs`)
      .then((r) => r.json())
      .then((data: Record<string, ShipStaticSpec>) => {
        const normalized: Record<string, ShipStaticSpec> = {};
        for (const [key, value] of Object.entries(data)) {
          normalized[key.toLowerCase()] = value;
        }
        setSpecs(normalized);
      })
      .catch(() => {});
  }, []);

  return specs;
}

export function lookupShipSpec(specs: Record<string, ShipStaticSpec>, shipType: string | null | undefined): ShipStaticSpec | undefined {
  return shipType ? specs[shipType.toLowerCase()] : undefined;
}
