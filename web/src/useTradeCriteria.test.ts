import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, renderHook } from "@testing-library/react";
import type { TradeSearchDefaults } from "./api";
import { useTradeCriteria } from "./useTradeCriteria";

const STORAGE_KEY = "trade.criteriaOverrides.v1";

afterEach(cleanup);

function makeDefaults(over: Partial<TradeSearchDefaults> = {}): TradeSearchDefaults {
  return {
    cargoCapacity: 192,
    credits: 1_000_000,
    shipJumpRangeLy: 30.5,
    originSystemName: "Sol",
    requiredShipSizeClass: 3,
    maxHops: 3,
    maxJumpsFromOrigin: 5,
    maxJumpsBetweenStations: 2,
    includeUnknownPadSize: true,
    excludePlanetary: true,
    excludeFleetCarriers: true,
    ...over,
  };
}

beforeEach(() => {
  localStorage.clear();
});

describe("useTradeCriteria", () => {
  it("initializes from detected defaults when nothing is stored", () => {
    const { result, rerender } = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: { d: null as TradeSearchDefaults | null },
    });
    expect(result.current.criteria).toBeNull();

    rerender({ d: makeDefaults() });

    expect(result.current.criteria).toMatchObject({
      cargoCapacity: 192,
      credits: 1_000_000,
      shipJumpRangeLy: 30.5,
      originSystemName: "Sol",
      originStationId: null,
      originMarketId: null,
    });
  });

  it("keeps edited fields across remounts while untouched fields track the current ship", () => {
    const first = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: { d: makeDefaults() },
    });
    first.result.current.patch({ cargoCapacity: 64, maxHops: 2 });
    first.unmount();

    // Returning to the Trade tab after swapping ships: fresh defaults arrive,
    // and only the fields the user never touched should follow them.
    const second = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: {
        d: makeDefaults({
          cargoCapacity: 256,
          credits: 5_000_000,
          shipJumpRangeLy: 41.2,
          originSystemName: "Shinrarta Dezhra",
        }),
      },
    });

    expect(second.result.current.criteria).toMatchObject({
      cargoCapacity: 64, // edited: sticks
      maxHops: 2, // edited: sticks
      credits: 5_000_000, // untouched: tracks ship
      shipJumpRangeLy: 41.2, // untouched: tracks ship
      originSystemName: "Shinrarta Dezhra", // not user-editable: always tracks
    });
  });

  it("reset to detected clears all overrides", () => {
    const first = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: { d: makeDefaults() },
    });
    first.result.current.patch({ cargoCapacity: 64 });
    first.result.current.resetToDefaults();
    expect(result_criteria(first)).toMatchObject({ cargoCapacity: 192 });
    first.unmount();

    const second = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: { d: makeDefaults({ cargoCapacity: 256 }) },
    });
    expect(second.result.current.criteria).toMatchObject({ cargoCapacity: 256 });

    function result_criteria(h: typeof first) {
      return h.result.current.criteria!;
    }
  });

  it("ignores corrupted storage and falls back to defaults", () => {
    localStorage.setItem(STORAGE_KEY, "{not json");

    const { result } = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: { d: makeDefaults() },
    });

    expect(result.current.criteria).toMatchObject({ cargoCapacity: 192 });
  });

  it("never lets a stored override pin the detected origin system", () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ originSystemName: "Old System", cargoCapacity: 64 }));

    const { result } = renderHook(({ d }) => useTradeCriteria(d), {
      initialProps: { d: makeDefaults() },
    });

    expect(result.current.criteria).toMatchObject({
      cargoCapacity: 64,
      originSystemName: "Sol",
    });
  });
});
