import { useState } from "react";
import { useDashboardState } from "../useDashboardState";
import { useWishlist } from "../useWishlist";
import { useShipSpecs, lookupShipSpec } from "../useShipSpecs";
import { useModuleSpecs, moduleDisplayParts, moduleSlotLabel } from "../useModuleSpecs";
import { useShipLoadout } from "../useShipLoadout";
import { shipImageUrl, sizeClassLabel } from "../api";
import { formatCredits, shipLocationText } from "../shipLabels";

export default function FleetPage() {
  const { state } = useDashboardState();
  const { items: wishlist, add, markPurchased, remove } = useWishlist();
  const specs = useShipSpecs();
  const moduleSpecs = useModuleSpecs();
  const [selectedShipId, setSelectedShipId] = useState<number | null>(null);
  const [newShipType, setNewShipType] = useState("");
  const [newDisplayName, setNewDisplayName] = useState("");
  const [newCost, setNewCost] = useState("");

  // No explicit selection yet -> show the ship being flown. Computed before
  // the loading guard so the hook below runs unconditionally.
  const currentShipId = state?.fleet.find((s) => s.location === "Current")?.shipId;
  const selectedId = selectedShipId ?? currentShipId ?? state?.fleet[0]?.shipId ?? null;
  const { loadout, notFound } = useShipLoadout(selectedId);

  if (!state) {
    return <p className="loading">Waiting for fleet data…</p>;
  }

  const selectedShip = state.fleet.find((s) => s.shipId === selectedId) ?? null;

  const handleAdd = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newShipType.trim()) return;
    add(newShipType.trim(), newDisplayName.trim() || newShipType.trim(), newCost ? Number(newCost) : null);
    setNewShipType("");
    setNewDisplayName("");
    setNewCost("");
  };

  return (
    <div className="grid">
      <section className="card wide">
        <h2>Fleet ({state.fleet.length})</h2>
        <div className="ship-grid">
          {state.fleet.map((ship) => {
            const spec = lookupShipSpec(specs, ship.shipType);
            return (
              <button
                type="button"
                className={`ship-card${ship.shipId === selectedId ? " selected" : ""}`}
                key={ship.shipId}
                onClick={() => setSelectedShipId(ship.shipId)}
              >
                <div className="ship-card-side">
                  <img
                    className="ship-badge"
                    src={shipImageUrl(ship.shipType ?? "unknown", ship.shipTypeDisplay ?? ship.shipType, spec?.sizeClass, ship.name)}
                    alt={ship.shipTypeDisplay ?? ship.shipType ?? "Unknown ship"}
                  />
                  {ship.location === "Current" ? <span className="chip legal-clean ship-active-chip">active</span> : null}
                </div>
                <div className="ship-card-body">
                  <div className="ship-card-name">
                    {ship.name || ship.shipTypeDisplay || ship.shipType}
                  </div>
                  <div className="ship-card-type">{ship.shipTypeDisplay ?? ship.shipType}</div>
                  <div className="ship-card-location">
                    {shipLocationText(ship, { system: state.currentSystem, station: state.currentStation })}
                  </div>
                  {spec ? (
                    <div className="ship-card-specs">
                      {spec.manufacturer ? `${spec.manufacturer} · ` : ""}
                      {sizeClassLabel(spec.sizeClass)} · {spec.hullMass.toFixed(0)}T hull
                    </div>
                  ) : null}
                </div>
              </button>
            );
          })}
        </div>
      </section>

      <section className="card wide">
        <h2>
          Ship loadout
          {selectedShip ? ` — ${selectedShip.name || selectedShip.shipTypeDisplay || selectedShip.shipType}` : ""}
        </h2>
        {!selectedShip ? (
          <p className="empty-hint">No ships in your fleet yet.</p>
        ) : loadout ? (
          <>
            <p className="loadout-seen">Last seen {new Date(loadout.seenAtUtc).toLocaleString()}</p>
            <div className="loadout-stats">
              <div className="loadout-stat">
                <span className="loadout-stat-label">Cargo capacity</span>
                <span className="loadout-stat-value">{loadout.cargoCapacity} T</span>
              </div>
              <div className="loadout-stat">
                <span className="loadout-stat-label">Max jump range</span>
                <span className="loadout-stat-value">{loadout.maxJumpRange.toFixed(2)} LY</span>
              </div>
              <div className="loadout-stat">
                <span className="loadout-stat-label">Fuel tank</span>
                <span className="loadout-stat-value">{loadout.fuelCapacityMain} T</span>
              </div>
              <div className="loadout-stat">
                <span className="loadout-stat-label">Hull value</span>
                <span className="loadout-stat-value">{formatCredits(loadout.hullValue)}</span>
              </div>
              <div className="loadout-stat">
                <span className="loadout-stat-label">Modules value</span>
                <span className="loadout-stat-value">{formatCredits(loadout.modulesValue)}</span>
              </div>
              <div className="loadout-stat">
                <span className="loadout-stat-label">Rebuy</span>
                <span className="loadout-stat-value">{formatCredits(loadout.rebuy)}</span>
              </div>
            </div>
            {loadout.modules.map((m, i) => {
              const parts = moduleDisplayParts(moduleSpecs, m.item);
              return (
                <div className="card-row" key={`${m.slot}-${i}`}>
                  <span className="label">
                    {moduleSlotLabel(m.slot)}
                    {!m.on ? <span className="chip legal-caution module-off-chip">off</span> : null}
                  </span>
                  <span className="value">
                    {parts.name}
                    {parts.mount ? <span className="txn-time"> ({parts.mount})</span> : null}
                    {parts.classRating ? <span className="chip module-class-chip">{parts.classRating}</span> : null}
                    {m.engineeringBlueprint ? (
                      <span className="chip module-eng-chip">
                        {m.engineeringBlueprint} G{m.engineeringLevel}
                      </span>
                    ) : null}
                  </span>
                </div>
              );
            })}
          </>
        ) : notFound ? (
          <p className="empty-hint">
            No loadout recorded for this ship yet - it's captured automatically when you board it in game.
          </p>
        ) : (
          <p className="empty-hint">Loading loadout…</p>
        )}
      </section>

      <section className="card wide">
        <h2>Wishlist</h2>
        <form className="wishlist-form" onSubmit={handleAdd}>
          <input
            placeholder="Ship type (e.g. Anaconda)"
            value={newShipType}
            onChange={(e) => setNewShipType(e.target.value)}
          />
          <input
            placeholder="Display name (optional)"
            value={newDisplayName}
            onChange={(e) => setNewDisplayName(e.target.value)}
          />
          <input
            placeholder="Est. cost"
            inputMode="numeric"
            value={newCost}
            onChange={(e) => setNewCost(e.target.value.replace(/\D/g, ""))}
          />
          <button type="submit">Add</button>
        </form>

        {wishlist.length === 0 ? (
          <p className="empty-hint">No ships on the wishlist yet.</p>
        ) : (
          wishlist.map((item) => (
            <div className="card-row" key={item.id}>
              <span className="label">
                {item.displayName ?? item.shipType}
                {item.estimatedCost ? <span className="txn-time"> · est. {formatCredits(item.estimatedCost)}</span> : null}
              </span>
              <span className="value wishlist-actions">
                <button className="link-button" onClick={() => markPurchased(item.id)}>
                  Bought it
                </button>
                <button className="link-button link-button-danger" onClick={() => remove(item.id)}>
                  Remove
                </button>
              </span>
            </div>
          ))
        )}
      </section>
    </div>
  );
}
