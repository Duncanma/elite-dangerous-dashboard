import { describe, expect, it } from "vitest";
import type { CategoryTotal } from "./api";
import { summarizeCategoryTotals } from "./economicsSummary";

function totals(...entries: CategoryTotal[]): CategoryTotal[] {
  return entries;
}

describe("summarizeCategoryTotals", () => {
  it("nets TradeBuy and TradeSell into a single Trade profit line", () => {
    const result = summarizeCategoryTotals(
      totals({ category: "TradeBuy", amount: -50_000 }, { category: "TradeSell", amount: 80_000 }),
    );

    expect(result.income).toEqual([{ label: "Trade profit", amount: 30_000 }]);
    expect(result.expenses).toEqual([]);
    expect(result.net).toBe(30_000);
  });

  it("splits categories into income/expenses by sign and sorts each by magnitude", () => {
    const result = summarizeCategoryTotals(
      totals(
        { category: "BountyVoucher", amount: 10_000 },
        { category: "MissionReward", amount: 50_000 },
        { category: "Repair", amount: -2_000 },
        { category: "Refuel", amount: -500 },
      ),
    );

    expect(result.income.map((l) => l.label)).toEqual(["Mission rewards", "Bounty"]);
    expect(result.expenses.map((l) => l.label)).toEqual(["Repairs", "Refuelling"]);
    expect(result.net).toBe(57_500);
  });

  it("drops categories that net to exactly zero, but still counts them in net", () => {
    const result = summarizeCategoryTotals(totals({ category: "Rearm", amount: 0 }, { category: "BountyVoucher", amount: 1_000 }));

    expect(result.income).toEqual([{ label: "Bounty", amount: 1_000 }]);
    expect(result.expenses).toEqual([]);
    expect(result.net).toBe(1_000);
  });

  it("returns an empty summary for no transactions in range", () => {
    expect(summarizeCategoryTotals([])).toEqual({ income: [], expenses: [], net: 0 });
  });
});
