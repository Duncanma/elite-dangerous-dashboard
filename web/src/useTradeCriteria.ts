import { useCallback, useEffect, useState } from "react";
import type { TradeSearchCriteria, TradeSearchDefaults } from "./api";

const STORAGE_KEY = "trade.criteriaOverrides.v1";

// Only fields the settings panel lets the user edit may be persisted. The
// rest (originStationId, originSystemName) derive from the detected ship and
// location and must always track fresh defaults.
const OVERRIDABLE_KEYS: readonly (keyof TradeSearchCriteria)[] = [
  "cargoCapacity",
  "credits",
  "maxHops",
  "shipJumpRangeLy",
  "maxJumpsFromOrigin",
  "maxJumpsBetweenStations",
  "requiredShipSizeClass",
  "originMarketId",
  "includeUnknownPadSize",
  "excludePlanetary",
  "excludeFleetCarriers",
];

type Overrides = Partial<TradeSearchCriteria>;

function loadOverrides(): Overrides {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) return {};
    const out: Overrides = {};
    for (const key of OVERRIDABLE_KEYS) {
      if (key in parsed) {
        (out as Record<string, unknown>)[key] = (parsed as Record<string, unknown>)[key];
      }
    }
    return out;
  } catch {
    return {};
  }
}

export function criteriaFromDefaults(defaults: TradeSearchDefaults): TradeSearchCriteria {
  return {
    originStationId: null,
    originMarketId: null,
    originSystemName: defaults.originSystemName,
    cargoCapacity: defaults.cargoCapacity || 1,
    credits: defaults.credits,
    maxHops: defaults.maxHops,
    shipJumpRangeLy: defaults.shipJumpRangeLy,
    maxJumpsFromOrigin: defaults.maxJumpsFromOrigin,
    maxJumpsBetweenStations: defaults.maxJumpsBetweenStations,
    requiredShipSizeClass: defaults.requiredShipSizeClass,
    includeUnknownPadSize: defaults.includeUnknownPadSize,
    excludePlanetary: defaults.excludePlanetary,
    excludeFleetCarriers: defaults.excludeFleetCarriers,
  };
}

// Trade criteria are page-local state, and React Router unmounts the Trade
// page on every tab switch — so without persistence, every navigation
// silently re-seeded the settings from the detected ship. This hook persists
// only user-edited fields as localStorage overrides; untouched fields keep
// tracking the current ship/location whenever fresh defaults arrive.
export function useTradeCriteria(defaults: TradeSearchDefaults | null) {
  const [criteria, setCriteria] = useState<TradeSearchCriteria | null>(null);

  useEffect(() => {
    if (!defaults) return;
    setCriteria({ ...criteriaFromDefaults(defaults), ...loadOverrides() });
  }, [defaults]);

  const patch = useCallback((p: Partial<TradeSearchCriteria>) => {
    setCriteria((c) => (c ? { ...c, ...p } : c));
    const next: Overrides = { ...loadOverrides() };
    for (const key of OVERRIDABLE_KEYS) {
      if (key in p) {
        (next as Record<string, unknown>)[key] = p[key];
      }
    }
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // Storage full or unavailable: keep the in-memory edit for this session.
    }
  }, []);

  const resetToDefaults = useCallback(() => {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
    if (defaults) {
      setCriteria(criteriaFromDefaults(defaults));
    }
  }, [defaults]);

  return { criteria, patch, resetToDefaults };
}
