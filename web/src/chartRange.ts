import { useState } from "react";

export type ChartRange = "30d" | "90d" | "all";

export const CHART_RANGES: { value: ChartRange; label: string }[] = [
  { value: "30d", label: "30 days" },
  { value: "90d", label: "90 days" },
  { value: "all", label: "All time" },
];

export interface TimeValuePoint {
  time: number;
}

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Applies a range filter, and for "all time" downsamples long histories by
 * bucketing into weeks or months - otherwise a multi-year history renders as
 * an unreadable wall of points. Values here (balance, total merits) are
 * running totals rather than per-period deltas, so downsampling keeps the
 * *last* point in each bucket rather than summing/averaging.
 */
export function filterAndGroup<T extends TimeValuePoint>(points: T[], range: ChartRange): T[] {
  if (points.length === 0) {
    return points;
  }

  if (range !== "all") {
    const days = range === "30d" ? 30 : 90;
    const cutoff = Date.now() - days * DAY_MS;
    return points.filter((p) => p.time >= cutoff);
  }

  const spanMs = points[points.length - 1].time - points[0].time;
  if (spanMs <= 90 * DAY_MS) {
    return points;
  }

  const bucketMs = spanMs <= 365 * DAY_MS ? 7 * DAY_MS : 30 * DAY_MS;
  const buckets = new Map<number, T>();
  for (const p of points) {
    // Points are chronological, so the last write per bucket key naturally
    // keeps that bucket's most recent point.
    buckets.set(Math.floor(p.time / bucketMs), p);
  }

  return Array.from(buckets.values()).sort((a, b) => a.time - b.time);
}

/**
 * True when picking this range wouldn't actually exclude any data - i.e. the
 * oldest point already falls within the window, so the chart would look
 * identical to "All time". Used to flag a range button as currently
 * meaningless rather than let it silently appear to "do nothing".
 */
export function rangeShowsFullHistory<T extends TimeValuePoint>(points: T[], range: ChartRange): boolean {
  if (range === "all" || points.length === 0) {
    return false;
  }

  const days = range === "30d" ? 30 : 90;
  const cutoff = Date.now() - days * DAY_MS;
  return points[0].time >= cutoff;
}

function isChartRange(value: string | null): value is ChartRange {
  return value === "30d" || value === "90d" || value === "all";
}

/**
 * Plain useState resets on every remount - and React Router unmounts a page
 * component whenever you navigate away, so switching tabs and back was
 * silently reverting the chart back to "All time". Backing it with
 * localStorage makes the choice survive both tab switches and page reloads.
 */
export function usePersistedChartRange(storageKey: string, fallback: ChartRange = "all"): [ChartRange, (range: ChartRange) => void] {
  const [range, setRangeState] = useState<ChartRange>(() => {
    const stored = typeof localStorage === "undefined" ? null : localStorage.getItem(storageKey);
    return isChartRange(stored) ? stored : fallback;
  });

  const setRange = (next: ChartRange) => {
    setRangeState(next);
    try {
      localStorage.setItem(storageKey, next);
    } catch {
      // Ignore (e.g. storage disabled/full) - the in-memory value still works for this session.
    }
  };

  return [range, setRange];
}

export function formatHistorySpan<T extends TimeValuePoint>(points: T[]): string {
  if (points.length < 2) {
    return "less than a day";
  }

  const days = Math.round((points[points.length - 1].time - points[0].time) / DAY_MS);
  if (days < 1) {
    return "less than a day";
  }

  return days === 1 ? "1 day" : `${days} days`;
}
