import { describe, expect, it } from "vitest";
import { moduleSlotLabel } from "./useModuleSpecs";

describe("moduleSlotLabel", () => {
  it("labels sized hardpoints", () => {
    expect(moduleSlotLabel("SmallHardpoint1")).toBe("Small Hardpoint 1");
    expect(moduleSlotLabel("MediumHardpoint2")).toBe("Medium Hardpoint 2");
    expect(moduleSlotLabel("LargeHardpoint1")).toBe("Large Hardpoint 1");
    expect(moduleSlotLabel("HugeHardpoint2")).toBe("Huge Hardpoint 2");
  });

  it("labels tiny hardpoints as utility mounts", () => {
    expect(moduleSlotLabel("TinyHardpoint1")).toBe("Utility Mount 1");
    expect(moduleSlotLabel("TinyHardpoint8")).toBe("Utility Mount 8");
  });

  it("labels core internals with in-game names", () => {
    expect(moduleSlotLabel("Armour")).toBe("Armour");
    expect(moduleSlotLabel("PowerPlant")).toBe("Power Plant");
    expect(moduleSlotLabel("MainEngines")).toBe("Thrusters");
    expect(moduleSlotLabel("FrameShiftDrive")).toBe("Frame Shift Drive");
    expect(moduleSlotLabel("LifeSupport")).toBe("Life Support");
    expect(moduleSlotLabel("PowerDistributor")).toBe("Power Distributor");
    expect(moduleSlotLabel("Radar")).toBe("Sensors");
    expect(moduleSlotLabel("FuelTank")).toBe("Fuel Tank");
    expect(moduleSlotLabel("CargoHatch")).toBe("Cargo Hatch");
    expect(moduleSlotLabel("PlanetaryApproachSuite")).toBe("Planetary Approach Suite");
  });

  it("labels optional internal slots with size", () => {
    expect(moduleSlotLabel("Slot02_Size7")).toBe("Internal Slot 2 (Size 7)");
    expect(moduleSlotLabel("Slot10_Size3")).toBe("Internal Slot 10 (Size 3)");
  });

  it("labels military compartments", () => {
    expect(moduleSlotLabel("Military01")).toBe("Military Compartment 1");
    expect(moduleSlotLabel("Military03")).toBe("Military Compartment 3");
  });

  it("labels cosmetic slots", () => {
    expect(moduleSlotLabel("PaintJob")).toBe("Paint Job");
    expect(moduleSlotLabel("Decal0")).toBe("Decal 1");
    expect(moduleSlotLabel("Decal2")).toBe("Decal 3");
    expect(moduleSlotLabel("Bobble01")).toBe("Bobblehead 1");
    expect(moduleSlotLabel("Bobble10")).toBe("Bobblehead 10");
    expect(moduleSlotLabel("ShipName0")).toBe("Ship Name Plate 1");
    expect(moduleSlotLabel("ShipName1")).toBe("Ship Name Plate 2");
    expect(moduleSlotLabel("ShipID0")).toBe("Ship ID Plate");
    expect(moduleSlotLabel("VesselVoice")).toBe("Voice (COVAS)");
    expect(moduleSlotLabel("ShipKitSpoiler")).toBe("Ship Kit — Spoiler");
    expect(moduleSlotLabel("ShipKitWings")).toBe("Ship Kit — Wings");
    expect(moduleSlotLabel("ShipKitBumper")).toBe("Ship Kit — Bumper");
    expect(moduleSlotLabel("ShipKitTail")).toBe("Ship Kit — Tail");
  });

  it("splits unrecognized camel-case names instead of showing them raw", () => {
    expect(moduleSlotLabel("WhateverThing2")).toBe("Whatever Thing 2");
  });

  it("handles missing slot ids", () => {
    expect(moduleSlotLabel(null)).toBe("Unknown slot");
    expect(moduleSlotLabel(undefined)).toBe("Unknown slot");
  });
});
