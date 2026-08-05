import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import type { DashboardState } from "./api";
import FleetPage from "./pages/FleetPage";

// vitest globals are off, so RTL's auto-cleanup doesn't register - without
// this, renders accumulate across tests and screen queries see every mount.
afterEach(cleanup);

const fleet = [
  {
    shipId: 1,
    shipType: "krait_mkii",
    shipTypeDisplay: "Krait Mk II",
    name: "Unreasonably Long Ship Name",
    starSystem: "Sol",
    location: "Current",
  },
  {
    shipId: 2,
    shipType: "sidewinder",
    shipTypeDisplay: "Sidewinder",
    name: null,
    starSystem: "Alpha Centauri",
    location: "Remote",
  },
];

const dashboardState = {
  fleet,
  currentSystem: "Sol",
  currentStation: null,
  currentShipModules: [],
} as unknown as DashboardState;

const loadoutForShip1 = {
  shipId: 1,
  shipType: "krait_mkii",
  seenAtUtc: "2026-08-03T15:24:00Z",
  cargoCapacity: 192,
  maxJumpRange: 41.25,
  fuelCapacityMain: 32,
  unladenMass: 350,
  rebuy: 19_000_000,
  hullValue: 380_000_000,
  modulesValue: 120_000_000,
  modules: [
    {
      slot: "LifeSupport",
      item: "int_lifesupport_size5_class_a",
      on: true,
      priority: 0,
      health: 1,
      engineeringBlueprint: "Overcharged",
      engineeringLevel: 5,
      experimentalEffect: null,
    },
  ],
};

const useShipLoadoutMock = vi.fn((shipId: number | null) =>
  shipId === 1 ? { loadout: loadoutForShip1, notFound: false } : { loadout: null, notFound: true },
);

vi.mock("./useDashboardState", () => ({
  useDashboardState: () => ({ state: dashboardState, connected: true }),
}));
vi.mock("./useWishlist", () => ({
  useWishlist: () => ({ items: [], add: vi.fn(), markPurchased: vi.fn(), remove: vi.fn() }),
}));
vi.mock("./useShipSpecs", () => ({
  useShipSpecs: () => [],
  lookupShipSpec: () => undefined,
}));
vi.mock("./useModuleSpecs", () => ({
  useModuleSpecs: () => [],
  moduleDisplayParts: (_specs: unknown, item: string) =>
    item === "int_lifesupport_size5_class_a"
      ? { name: "Life Support", mount: null, classRating: "5A" }
      : { name: item, mount: null, classRating: null },
  moduleSlotLabel: (slot: string) => slot,
}));
vi.mock("./useShipLoadout", () => ({
  useShipLoadout: (shipId: number | null) => useShipLoadoutMock(shipId),
}));

describe("FleetPage active chip", () => {
  it("renders under the ship image, not in the name row where it can be clipped", () => {
    render(<FleetPage />);

    const chip = screen.getByText("active");
    expect(chip.closest(".ship-card-name")).toBeNull();
    expect(chip.parentElement?.className).toContain("ship-card-side");
  });

  it("only marks the ship currently being flown", () => {
    render(<FleetPage />);

    expect(screen.getAllByText("active")).toHaveLength(1);
  });
});

describe("FleetPage ship loadout panel", () => {
  it("shows the current ship's loadout by default, with ship figures and last-seen time", () => {
    render(<FleetPage />);

    expect(useShipLoadoutMock).toHaveBeenCalledWith(1);
    expect(screen.getByRole("heading", { name: "Ship loadout — Unreasonably Long Ship Name" })).toBeTruthy();
    expect(screen.getByText(/Last seen/)).toBeTruthy();
    expect(screen.getByText("192 T")).toBeTruthy();
    expect(screen.getByText("41.25 LY")).toBeTruthy();
    expect(screen.getByText("380 M")).toBeTruthy();
    expect(screen.getByText("Life Support")).toBeTruthy();
  });

  it("selecting another ship card loads its loadout and shows a hint when none was recorded", () => {
    render(<FleetPage />);

    fireEvent.click(screen.getByRole("button", { name: /Sidewinder/ }));

    expect(useShipLoadoutMock).toHaveBeenLastCalledWith(2);
    expect(screen.getByRole("heading", { name: "Ship loadout — Sidewinder" })).toBeTruthy();
    expect(screen.getByText(/No loadout recorded/)).toBeTruthy();
  });
});

describe("FleetPage loadout module display", () => {
  it("renders class/rating and engineering as chips, not inline text", () => {
    render(<FleetPage />);

    const classChip = screen.getByText("5A");
    expect(classChip.className).toContain("module-class-chip");
    expect(screen.getByText("Overcharged G5").className).toContain("module-eng-chip");
    // The name text node itself must not carry the old flattened "— 5A" suffix.
    expect(classChip.parentElement?.firstChild?.textContent).toBe("Life Support");
  });
});
