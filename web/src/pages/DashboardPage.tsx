import { useDashboardState } from "../useDashboardState";
import { useTrade } from "../useTrade";
import { useShipSpecs, lookupShipSpec } from "../useShipSpecs";
import { shipImageUrl } from "../api";
import { formatCredits, shipLocationText } from "../shipLabels";
import { rankLabel } from "../rankLabels";
import { factionIconUrl } from "../factionArt";
import { MaskIcon } from "../MaskIcon";

function legalStateClass(legalState: string | null): string {
  if (!legalState || legalState === "Clean") return "legal-clean";
  if (legalState === "Speeding" || legalState === "IllegalCargo") return "legal-caution";
  return "legal-wanted";
}

const RANK_ROWS: { key: keyof typeof rankLabel; label: string; faction?: string }[] = [
  { key: "combat", label: "Combat" },
  { key: "trade", label: "Trade" },
  { key: "explore", label: "Exploration" },
  { key: "cqc", label: "CQC" },
  { key: "federation", label: "Federation", faction: "federation" },
  { key: "empire", label: "Empire", faction: "empire" },
];

const SPEND_SEGMENTS = [
  { key: "spentOnShips", label: "Ships", color: "#aa3bff" },
  { key: "spentOnOutfitting", label: "Outfitting", color: "#c084fc" },
  { key: "spentOnRepairs", label: "Repairs", color: "#f97316" },
  { key: "spentOnFuel", label: "Fuel", color: "#eab308" },
  { key: "spentOnAmmoConsumables", label: "Ammo/consumables", color: "#38bdf8" },
  { key: "insuranceClaims", label: "Insurance", color: "#ef4444" },
] as const;

export default function DashboardPage() {
  const { state } = useDashboardState();
  const { activePlan } = useTrade();
  const specs = useShipSpecs();

  if (!state) {
    return <p className="loading">Waiting for commander data…</p>;
  }

  const spec = lookupShipSpec(specs, state.shipType);
  const rankProgressKey = {
    combat: "combatProgress",
    trade: "tradeProgress",
    explore: "exploreProgress",
    cqc: "cqcProgress",
    federation: "federationProgress",
    empire: "empireProgress",
  } as const;

  const spendTotal = SPEND_SEGMENTS.reduce((sum, seg) => sum + (state.economy[seg.key] ?? 0), 0);

  return (
    <div className="grid">
      <section className="card">
        <h2>Commander</h2>
        <div className="ship-portrait">
          <img
            className="ship-portrait-image"
            src={shipImageUrl(state.shipType ?? "unknown", state.shipTypeDisplay ?? state.shipType, spec?.sizeClass, state.shipName)}
            alt={state.shipTypeDisplay ?? state.shipType ?? "Unknown ship"}
          />
          <div>
            <div className="ship-portrait-name">{state.shipName || state.shipTypeDisplay || state.shipType}</div>
            <div className="ship-portrait-type">
              {state.shipTypeDisplay ?? state.shipType}
              {state.shipIdent ? ` [${state.shipIdent}]` : ""}
            </div>
          </div>
        </div>
        <div className="card-row">
          <span className="label">Commander</span>
          <span className="value">{state.name ?? "Unknown"}</span>
        </div>
        <div className="card-row">
          <span className="label">Location</span>
          <span className="value">
            {state.currentSystem ?? "Unknown"}
            {state.docked && state.currentStation ? ` — ${state.currentStation}` : ""}
          </span>
        </div>
        <div className="card-row">
          <span className="label">Jump range / Cargo</span>
          <span className="value">
            {state.maxJumpRange.toFixed(1)} LY / {state.cargoCapacity.toFixed(0)} T capacity
          </span>
        </div>
        <div className="card-row">
          <span className="label">Fuel</span>
          <span className="value">
            {state.shipStatus.fuelMain.toFixed(1)} T
          </span>
        </div>
        <div className="card-row">
          <span className="label">Legal status</span>
          <span className={`value chip ${legalStateClass(state.shipStatus.legalState)}`}>
            {state.shipStatus.legalState ?? "Clean"}
          </span>
        </div>
      </section>

      <section className="card">
        <h2>Rank</h2>
        {RANK_ROWS.map((r) => {
          const tier = state.rank[r.key];
          const progress = state.rank[rankProgressKey[r.key]];
          const iconUrl = r.faction ? factionIconUrl(r.faction) : null;
          return (
            <div className="rank-row" key={r.key}>
              <div className="rank-row-header">
                <span className="label">
                  {iconUrl ? <MaskIcon src={iconUrl} className="inline-icon" label={r.label} /> : null}
                  {r.label}
                </span>
                <span className="value">{rankLabel[r.key](tier)}</span>
              </div>
              <div className="progress-track rank-bar-track">
                <div className="progress-fill" style={{ width: `${progress}%` }} />
              </div>
            </div>
          );
        })}
      </section>

      <section className="card">
        <h2>Economic status</h2>
        <div className="credits-hero">{formatCredits(state.economy.credits)}</div>
        <div className="card-row muted">
          <span className="label">Net worth</span>
          <span className="value">{formatCredits(state.economy.currentWealth)}</span>
        </div>
        {spendTotal > 0 ? (
          <>
            <div className="spend-bar">
              {SPEND_SEGMENTS.filter((seg) => state.economy[seg.key] > 0).map((seg) => (
                <div
                  key={seg.key}
                  className="spend-bar-segment"
                  style={{ width: `${((state.economy[seg.key] ?? 0) / spendTotal) * 100}%`, background: seg.color }}
                  title={`${seg.label}: ${formatCredits(state.economy[seg.key])}`}
                />
              ))}
            </div>
            <div className="spend-legend">
              {SPEND_SEGMENTS.filter((seg) => state.economy[seg.key] > 0).map((seg) => (
                <span className="spend-legend-item" key={seg.key}>
                  <span className="spend-legend-swatch" style={{ background: seg.color }} />
                  {seg.label}
                </span>
              ))}
            </div>
          </>
        ) : null}
      </section>

      <section className="card">
        <h2>Fleet ({state.fleet.length})</h2>
        {state.fleet.length === 0 ? (
          <p className="empty-hint">No ships found yet.</p>
        ) : (
          state.fleet.map((ship) => (
            <div className="card-row" key={ship.shipId}>
              <span className="label">
                {ship.name ? ship.name : ship.shipTypeDisplay ?? ship.shipType ?? "Unknown ship"}
                {ship.location === "Current" ? " (active)" : ""}
              </span>
              <span className="value">
                {shipLocationText(ship, { system: state.currentSystem, station: state.currentStation })}
              </span>
            </div>
          ))
        )}
      </section>

      <section className="card">
        <h2>Trade route</h2>
        {!activePlan ? (
          <p className="empty-hint">No active trade route. Start one from the Trade tab.</p>
        ) : (
          <>
            <div className="card-row">
              <span className="label">Progress</span>
              <span className="value">
                {activePlan.hops.filter((h) => h.soldUtc).length} / {activePlan.hops.length} hops complete
              </span>
            </div>
            <div className="card-row">
              <span className="label">Estimated profit</span>
              <span className="value amount-positive">{formatCredits(activePlan.estimatedProfit)}</span>
            </div>
            {activePlan.hops
              .filter((h) => !h.soldUtc)
              .slice(0, 1)
              .map((h) => (
                <div className="card-row muted" key={h.id}>
                  <span className="label">Next</span>
                  <span className="value">
                    {h.commodityName}: {h.originStationName} → {h.destinationStationName}
                  </span>
                </div>
              ))}
          </>
        )}
      </section>

      <div className="card-row muted last-updated">
        <span className="label">Last updated</span>
        <span className="value">{new Date(state.lastUpdatedUtc).toLocaleString()}</span>
      </div>
    </div>
  );
}
