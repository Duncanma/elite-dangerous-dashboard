import type { EconomicSnapshot } from "./api";
import { formatCredits } from "./shipLabels";

// Shared by the Dashboard's compact "Economic status" card and the
// Economics page's full-width summary (see issue #2) - one definition so
// the segments/colors in both places can never drift apart.
export const SPEND_SEGMENTS = [
  { key: "spentOnShips", label: "Ships", color: "#aa3bff" },
  { key: "spentOnOutfitting", label: "Outfitting", color: "#c084fc" },
  { key: "spentOnRepairs", label: "Repairs", color: "#f97316" },
  { key: "spentOnFuel", label: "Fuel", color: "#eab308" },
  { key: "spentOnAmmoConsumables", label: "Ammo/consumables", color: "#38bdf8" },
  { key: "insuranceClaims", label: "Insurance", color: "#ef4444" },
] as const satisfies { key: keyof EconomicSnapshot; label: string; color: string }[];

export function spendTotal(economy: EconomicSnapshot): number {
  return SPEND_SEGMENTS.reduce((sum, seg) => sum + (economy[seg.key] ?? 0), 0);
}

/**
 * The spend-breakdown bar + legend. `showAmounts` renders each segment's
 * credit total inline in the legend - there's room for that on the wide
 * Economics page summary, but not on the Dashboard's narrow card, where the
 * amount is only in the bar segment's hover tooltip.
 */
export function SpendBreakdown({ economy, showAmounts = false }: { economy: EconomicSnapshot; showAmounts?: boolean }) {
  const total = spendTotal(economy);
  if (total <= 0) {
    return null;
  }

  const segments = SPEND_SEGMENTS.filter((seg) => economy[seg.key] > 0);

  return (
    <>
      <div className="spend-bar">
        {segments.map((seg) => (
          <div
            key={seg.key}
            className="spend-bar-segment"
            style={{ width: `${(economy[seg.key] / total) * 100}%`, background: seg.color }}
            title={`${seg.label}: ${formatCredits(economy[seg.key])}`}
          />
        ))}
      </div>
      <div className="spend-legend">
        {segments.map((seg) => (
          <span className="spend-legend-item" key={seg.key}>
            <span className="spend-legend-swatch" style={{ background: seg.color }} />
            {seg.label}
            {showAmounts ? <span className="spend-legend-amount">{formatCredits(economy[seg.key])}</span> : null}
          </span>
        ))}
      </div>
    </>
  );
}

/**
 * Full-width variant of the Dashboard's "Economic status" card, for the top
 * of the Economics page - a horizontal stat row (credits/net worth/total
 * spend) instead of stacked card-rows, and the spend bar's legend shows
 * amounts inline since there's room for it.
 */
export function EconomicSummaryRow({ economy }: { economy: EconomicSnapshot }) {
  const total = spendTotal(economy);

  return (
    <>
      <div className="econ-summary-stats">
        <div className="econ-summary-stat">
          <span className="label">Credits</span>
          <span className="value">{formatCredits(economy.credits)}</span>
        </div>
        <div className="econ-summary-stat">
          <span className="label">Net worth</span>
          <span className="value">{formatCredits(economy.currentWealth)}</span>
        </div>
        {total > 0 ? (
          <div className="econ-summary-stat">
            <span className="label">Total spend</span>
            <span className="value">{formatCredits(total)}</span>
          </div>
        ) : null}
      </div>
      <SpendBreakdown economy={economy} showAmounts />
    </>
  );
}
