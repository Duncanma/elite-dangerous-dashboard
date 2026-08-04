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

export interface ModuleDisplayParts {
  name: string;
  mount: string | null;
  classRating: string | null;
}

// Structured form of a module's display text so the UI can style the parts
// differently (e.g. class/rating as a chip) instead of one flattened string.
export function moduleDisplayParts(specs: Record<string, ModuleStaticSpec>, item: string | null | undefined): ModuleDisplayParts {
  const spec = lookupModuleSpec(specs, item);
  if (!spec) {
    return { name: item ?? "Unknown", mount: null, classRating: null };
  }
  return {
    name: spec.displayName,
    mount: spec.mount,
    classRating: spec.class && spec.rating ? `${spec.class}${spec.rating}` : null,
  };
}

export function moduleDisplayName(specs: Record<string, ModuleStaticSpec>, item: string | null | undefined): string {
  const { name, mount, classRating } = moduleDisplayParts(specs, item);
  const mountText = mount ? ` (${mount})` : "";
  const classRatingText = classRating ? ` — ${classRating}` : "";
  return `${name}${mountText}${classRatingText}`;
}

// Fixed-name slots from the Loadout event, labeled with in-game terminology.
const CORE_SLOT_LABELS: Record<string, string> = {
  Armour: "Armour",
  PowerPlant: "Power Plant",
  MainEngines: "Thrusters",
  FrameShiftDrive: "Frame Shift Drive",
  LifeSupport: "Life Support",
  PowerDistributor: "Power Distributor",
  Radar: "Sensors",
  FuelTank: "Fuel Tank",
  CargoHatch: "Cargo Hatch",
  PlanetaryApproachSuite: "Planetary Approach Suite",
  PaintJob: "Paint Job",
  ShipID0: "Ship ID Plate",
  VesselVoice: "Voice (COVAS)",
  ShipKitSpoiler: "Ship Kit — Spoiler",
  ShipKitWings: "Ship Kit — Wings",
  ShipKitBumper: "Ship Kit — Bumper",
  ShipKitTail: "Ship Kit — Tail",
};

// The journal writes raw slot ids ("LargeHardpoint1", "Slot02_Size7"); render
// them the way the game UI refers to them.
export function moduleSlotLabel(slot: string | null | undefined): string {
  if (!slot) return "Unknown slot";

  const fixed = CORE_SLOT_LABELS[slot];
  if (fixed) return fixed;

  let match = /^(Tiny|Small|Medium|Large|Huge)Hardpoint(\d+)$/.exec(slot);
  if (match) {
    return match[1] === "Tiny" ? `Utility Mount ${Number(match[2])}` : `${match[1]} Hardpoint ${Number(match[2])}`;
  }

  match = /^Slot(\d+)_Size(\d+)$/.exec(slot);
  if (match) return `Internal Slot ${Number(match[1])} (Size ${match[2]})`;

  match = /^Military(\d+)$/.exec(slot);
  if (match) return `Military Compartment ${Number(match[1])}`;

  match = /^Decal(\d+)$/.exec(slot);
  if (match) return `Decal ${Number(match[1]) + 1}`;

  match = /^Bobble(\d+)$/.exec(slot);
  if (match) return `Bobblehead ${Number(match[1])}`;

  match = /^ShipName(\d+)$/.exec(slot);
  if (match) return `Ship Name Plate ${Number(match[1]) + 1}`;

  return slot
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/([A-Za-z])(\d)/g, "$1 $2");
}
