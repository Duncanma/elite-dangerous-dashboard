import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CHART_RANGES, filterAndGroup, rangeShowsFullHistory, type TimeValuePoint } from "./chartRange";

const DAY_MS = 24 * 60 * 60 * 1000;
const NOW = new Date("2026-08-07T12:00:00Z").getTime();

// Points at fixed ages (in days) before NOW - easier to reason about than
// absolute timestamps when asserting range cutoffs.
function pointsAgeDays(...daysAgo: number[]): TimeValuePoint[] {
  return daysAgo.map((d) => ({ time: NOW - d * DAY_MS })).sort((a, b) => a.time - b.time);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(NOW);
});

afterEach(() => {
  vi.useRealTimers();
});

describe("CHART_RANGES", () => {
  it("offers 7 days as the shortest, first-listed option", () => {
    expect(CHART_RANGES[0]).toEqual({ value: "7d", label: "7 days" });
    expect(CHART_RANGES.map((r) => r.value)).toEqual(["7d", "30d", "90d", "all"]);
  });
});

describe("filterAndGroup", () => {
  it("7d keeps only points within the last 7 days", () => {
    const points = pointsAgeDays(1, 6, 8, 20);
    const result = filterAndGroup(points, "7d");
    expect(result).toHaveLength(2);
    expect(result.every((p) => NOW - p.time <= 7 * DAY_MS)).toBe(true);
  });

  it("30d and 90d still use their own cutoffs after the shared lookup refactor", () => {
    const points = pointsAgeDays(1, 15, 45, 75, 100);
    expect(filterAndGroup(points, "30d")).toHaveLength(2);
    expect(filterAndGroup(points, "90d")).toHaveLength(4);
  });

  it("empty input is returned as-is for any range", () => {
    expect(filterAndGroup([], "7d")).toEqual([]);
  });
});

describe("rangeShowsFullHistory", () => {
  it("7d is redundant when the oldest point is already within the last 7 days", () => {
    expect(rangeShowsFullHistory(pointsAgeDays(1, 3), "7d")).toBe(true);
  });

  it("7d is not redundant once history extends past 7 days", () => {
    expect(rangeShowsFullHistory(pointsAgeDays(1, 10), "7d")).toBe(false);
  });

  it("all time is never redundant", () => {
    expect(rangeShowsFullHistory(pointsAgeDays(1), "all")).toBe(false);
  });
});
