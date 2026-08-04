import { useEffect, useState } from "react";
import { useTrade } from "../useTrade";
import { formatCredits } from "../shipLabels";
import { sizeClassLabel, type TradeSearchCriteria } from "../api";
import { stationIconUrl } from "../stationArt";
import { MaskIcon } from "../MaskIcon";

function StationIcon({ stationType }: { stationType: string | null }) {
  const url = stationIconUrl(stationType);
  return url ? <MaskIcon src={url} className="inline-icon" label={stationType ?? undefined} /> : null;
}

function criteriaFromDefaults(defaults: NonNullable<ReturnType<typeof useTrade>["defaults"]>): TradeSearchCriteria {
  return {
    originStationId: null,
    originMarketId: null,
    originSystemName: defaults.originSystemName,
    cargoCapacity: defaults.cargoCapacity || 1,
    credits: defaults.credits,
    maxHops: defaults.maxHops,
    shipJumpRangeLy: defaults.shipJumpRangeLy,
    maxJumpsFromOrigin: defaults.maxJumpsFromOrigin,
    maxJumpsBetweenStations: defaults.maxJumpsBetweenStations,
    requiredShipSizeClass: defaults.requiredShipSizeClass,
    includeUnknownPadSize: defaults.includeUnknownPadSize,
    excludePlanetary: defaults.excludePlanetary,
    excludeFleetCarriers: defaults.excludeFleetCarriers,
  };
}

export default function TradePage() {
  const { status, activePlan, candidates, defaults, coverage, searching, error, search, activate, abandon } = useTrade();
  const [criteria, setCriteria] = useState<TradeSearchCriteria | null>(null);
  const [showSettings, setShowSettings] = useState(false);

  useEffect(() => {
    if (defaults && !criteria) {
      setCriteria(criteriaFromDefaults(defaults));
    }
  }, [defaults, criteria]);

  const patch = (p: Partial<TradeSearchCriteria>) => setCriteria((c) => (c ? { ...c, ...p } : c));

  if (!criteria) {
    return (
      <div className="grid">
        <section className="card wide">
          <p className="empty-hint">Loading search defaults from your current ship and location…</p>
        </section>
      </div>
    );
  }

  return (
    <div className="grid">
      <section className="card wide">
        <h2>Live price feed</h2>
        <div className="card-row">
          <span className="label">EDDN status</span>
          <span className={`value chip ${status?.connected ? "legal-clean" : "legal-caution"}`}>
            {status?.connected ? "Connected" : "Connecting…"}
          </span>
        </div>
        <div className="card-row muted">
          <span className="label">Messages received / commodity updates</span>
          <span className="value">
            {status?.messagesReceived ?? 0} / {status?.commodityMessagesProcessed ?? 0}
          </span>
        </div>
        {status?.lastMessageAtUtc ? (
          <div className="card-row muted">
            <span className="label">Last message</span>
            <span className="value">{new Date(status.lastMessageAtUtc).toLocaleTimeString()}</span>
          </div>
        ) : null}
      </section>

      <section className="card wide">
        <h2>Route planner</h2>

        <button className="link-button settings-toggle" onClick={() => setShowSettings((s) => !s)}>
          {showSettings ? "Hide search settings" : "⚙ Search settings"}
        </button>

        {showSettings ? (
          <div className="settings-panel">
            <label className="settings-field">
              <span>Cargo capacity (T)</span>
              <input
                type="number"
                min={1}
                value={criteria.cargoCapacity}
                onChange={(e) => patch({ cargoCapacity: Number(e.target.value) || 1 })}
              />
            </label>
            <label className="settings-field">
              <span>Credits available</span>
              <input
                type="number"
                min={0}
                value={criteria.credits}
                onChange={(e) => patch({ credits: Number(e.target.value) || 0 })}
              />
            </label>
            <label className="settings-field">
              <span>Max hops</span>
              <input
                type="number"
                min={1}
                max={10}
                value={criteria.maxHops}
                onChange={(e) => patch({ maxHops: Number(e.target.value) || 1 })}
              />
            </label>
            <label className="settings-field">
              <span>Start within N jumps of {criteria.originSystemName ?? "current location"}</span>
              <input
                type="number"
                min={1}
                value={criteria.maxJumpsFromOrigin}
                onChange={(e) => patch({ maxJumpsFromOrigin: Number(e.target.value) || 1 })}
              />
            </label>
            <label className="settings-field">
              <span>Max jumps between stations</span>
              <input
                type="number"
                min={1}
                value={criteria.maxJumpsBetweenStations}
                onChange={(e) => patch({ maxJumpsBetweenStations: Number(e.target.value) || 1 })}
              />
            </label>
            <label className="settings-field">
              <span>Ship jump range (LY, fully laden)</span>
              <input
                type="number"
                min={0}
                step="0.1"
                value={criteria.shipJumpRangeLy}
                onChange={(e) => patch({ shipJumpRangeLy: Number(e.target.value) || 0 })}
              />
            </label>
            <label className="settings-field">
              <span>Required pad size</span>
              <select
                value={criteria.requiredShipSizeClass ?? ""}
                onChange={(e) => patch({ requiredShipSizeClass: e.target.value ? Number(e.target.value) : null })}
              >
                <option value="">Any</option>
                <option value={1}>{sizeClassLabel(1)}</option>
                <option value={2}>{sizeClassLabel(2)}</option>
                <option value={3}>{sizeClassLabel(3)}</option>
              </select>
            </label>
            <label className="settings-field">
              <span>Origin market ID (optional, overrides jump radius)</span>
              <input
                value={criteria.originMarketId ?? ""}
                onChange={(e) => patch({ originMarketId: e.target.value.replace(/\D/g, "") || null })}
              />
            </label>

            <label className="settings-checkbox">
              <input
                type="checkbox"
                checked={criteria.excludePlanetary}
                onChange={(e) => patch({ excludePlanetary: e.target.checked })}
              />
              No planetary stations
            </label>
            <label className="settings-checkbox">
              <input
                type="checkbox"
                checked={criteria.excludeFleetCarriers}
                onChange={(e) => patch({ excludeFleetCarriers: e.target.checked })}
              />
              No fleet carriers
            </label>
            <label className="settings-checkbox">
              <input
                type="checkbox"
                checked={criteria.includeUnknownPadSize}
                onChange={(e) => patch({ includeUnknownPadSize: e.target.checked })}
              />
              Include stations with unresolved pad size
            </label>

            <p className="settings-hint">
              Distances are straight-line, not plotted jump routes, and only cover systems we already know the
              location of. Pad size for stations you haven't personally docked at is inferred from station type where
              that's reliable; otherwise it's treated as unknown.
              {coverage ? (
                <>
                  {" "}
                  Currently {coverage.knownSystems.toLocaleString()} system
                  {coverage.knownSystems === 1 ? "" : "s"} with priced stations{" "}
                  {coverage.knownSystems === 1 ? "is" : "are"} placed on the map
                  {coverage.unresolvedSystems > 0
                    ? `, ${coverage.unresolvedSystems.toLocaleString()} not yet resolved.`
                    : "."}
                </>
              ) : null}
            </p>

            {defaults ? (
              <button className="link-button" onClick={() => setCriteria(criteriaFromDefaults(defaults))}>
                Reset to detected ship/location defaults
              </button>
            ) : null}
          </div>
        ) : null}

        <div className="wishlist-actions">
          <button className="primary-button" disabled={searching} onClick={() => search(criteria)}>
            {searching ? "Searching…" : "Find routes"}
          </button>
        </div>

        {error ? <p className="empty-hint amount-negative">{error}</p> : null}

        {candidates.length === 0 ? (
          <p className="empty-hint">No candidates yet - search above using live EDDN-fed prices.</p>
        ) : (
          <>
            {candidates.map((c, i) => (
              <div className="card-row" key={i}>
                <span className="label">
                  {c.commodityName}: <StationIcon stationType={c.originStationType} />
                  {c.originStationName} ({c.originSystemName}) → <StationIcon stationType={c.destinationStationType} />
                  {c.destinationStationName} ({c.destinationSystemName})
                  <span className="txn-time">
                    {" "}
                    · buy {formatCredits(-c.buyPrice)} / sell {formatCredits(c.sellPrice)} × {c.quantity}
                  </span>
                </span>
                <span className="value amount-positive">{formatCredits(c.totalProfit)}</span>
              </div>
            ))}
            <button className="primary-button" onClick={() => activate(criteria)}>
              Activate this route
            </button>
          </>
        )}
      </section>

      <section className="card wide">
        <h2>Active trade route</h2>
        {!activePlan ? (
          <p className="empty-hint">No active trade route.</p>
        ) : (
          <>
            <div className="card-row">
              <span className="label">Status</span>
              <span className="value chip legal-clean">{activePlan.status}</span>
            </div>
            <div className="card-row">
              <span className="label">Estimated profit</span>
              <span className="value amount-positive">{formatCredits(activePlan.estimatedProfit)}</span>
            </div>
            {activePlan.hops.map((h) => (
              <div className="card-row" key={h.id}>
                <span className="label">
                  {h.sequenceNumber + 1}. {h.commodityName}: <StationIcon stationType={h.originStationType} />
                  {h.originStationName} ({h.originSystemName}) → <StationIcon stationType={h.destinationStationType} />
                  {h.destinationStationName} ({h.destinationSystemName})
                  {h.soldUtc ? (
                    <span className="chip legal-clean module-off-chip">done</span>
                  ) : h.boughtUtc ? (
                    <span className="chip legal-caution module-off-chip">in transit</span>
                  ) : (
                    <span className="chip legal-wanted module-off-chip">pending</span>
                  )}
                </span>
                <span className="value">{formatCredits(h.plannedProfit)}</span>
              </div>
            ))}
            <button className="link-button link-button-danger abandon-button" onClick={abandon}>
              Abandon route
            </button>
          </>
        )}
      </section>
    </div>
  );
}
