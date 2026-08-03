import { useEffect, useState } from "react";
import { apiBaseUrl, type ModuleStaticSpec } from "./api";

/** Same case-insensitivity caveat as useShipSpecs - the Loadout event always writes module symbols lowercase. */
export function useModuleSpecs() {
  const [specs, setSpecs] = useState<Record<string, ModuleStaticSpec>>({});

  useEffect(() => {
    fetch(`${apiBaseUrl}/api/fleet/module-specs`)
      .then((r) => r.json())
      .then((data: Record<string, ModuleStaticSpec>) => {
        const normalized: Record<string, ModuleStaticSpec> = {};
        for (const [key, value] of Object.entries(data)) {
          normalized[key.toLowerCase()] = value;
        }
        setSpecs(normalized);
      })
      .catch(() => {});
  }, []);

  return specs;
}

export function lookupModuleSpec(specs: Record<string, ModuleStaticSpec>, item: string | null | undefined): ModuleStaticSpec | undefined {
  return item ? specs[item.toLowerCase()] : undefined;
}

export function moduleDisplayName(specs: Record<string, ModuleStaticSpec>, item: string | null | undefined): string {
  const spec = lookupModuleSpec(specs, item);
  if (!spec) {
    return item ?? "Unknown";
  }

  const mount = spec.mount ? ` (${spec.mount})` : "";
  const classRating = spec.class && spec.rating ? ` — ${spec.class}${spec.rating}` : "";
  return `${spec.displayName}${mount}${classRating}`;
}
