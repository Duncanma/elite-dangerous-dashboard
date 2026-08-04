import { describe, expect, it } from "vitest";
import type { ModuleStaticSpec } from "./api";
import { moduleDisplayName, moduleDisplayParts } from "./useModuleSpecs";

const specs: Record<string, ModuleStaticSpec> = {
  int_lifesupport_size5_class_a: {
    displayName: "Life Support",
    category: "internal",
    mount: null,
    class: 5,
    rating: "A",
  },
  hpt_pulselaser_fixed_small: {
    displayName: "Pulse Laser",
    category: "hardpoint",
    mount: "Fixed",
    class: 1,
    rating: "F",
  },
  decal_skull: {
    displayName: "Skull Decal",
    category: "cosmetic",
    mount: null,
    class: null,
    rating: null,
  },
};

describe("moduleDisplayParts", () => {
  it("splits name, mount and class/rating", () => {
    expect(moduleDisplayParts(specs, "int_lifesupport_size5_class_a")).toEqual({
      name: "Life Support",
      mount: null,
      classRating: "5A",
    });
    expect(moduleDisplayParts(specs, "hpt_pulselaser_fixed_small")).toEqual({
      name: "Pulse Laser",
      mount: "Fixed",
      classRating: "1F",
    });
  });

  it("leaves class/rating null when the spec has none", () => {
    expect(moduleDisplayParts(specs, "decal_skull")).toEqual({
      name: "Skull Decal",
      mount: null,
      classRating: null,
    });
  });

  it("falls back to the raw item for unknown modules", () => {
    expect(moduleDisplayParts(specs, "hpt_some_new_thing")).toEqual({
      name: "hpt_some_new_thing",
      mount: null,
      classRating: null,
    });
    expect(moduleDisplayParts(specs, null)).toEqual({
      name: "Unknown",
      mount: null,
      classRating: null,
    });
  });

  it("moduleDisplayName still composes the same flat string", () => {
    expect(moduleDisplayName(specs, "int_lifesupport_size5_class_a")).toBe("Life Support — 5A");
    expect(moduleDisplayName(specs, "hpt_pulselaser_fixed_small")).toBe("Pulse Laser (Fixed) — 1F");
    expect(moduleDisplayName(specs, "decal_skull")).toBe("Skull Decal");
  });
});
