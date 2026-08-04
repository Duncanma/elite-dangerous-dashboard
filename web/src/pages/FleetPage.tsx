import { useState } from "react";
import { useDashboardState } from "../useDashboardState";
import { useWishlist } from "../useWishlist";
import { useShipSpecs, lookupShipSpec } from "../useShipSpecs";
import { useModuleSpecs, moduleDisplayName } from "../useModuleSpecs";
import { shipImageUrl, sizeClassLabel } from "../api";
import { formatCredits, shipLocationText } from "../shipLabels";

export default function FleetPage() {
  const { state } = useDashboardState();
  const { items: wishlist, add, markPurchased, remove } = useWishlist();
  const specs = useShipSpecs();
  const moduleSpecs = useModuleSpecs();
  const [newShipType, setNewShipType] = useState("");
  const [newDisplayName, setNewDisplayName] = useState("");
  const [newCost, setNewCost] = useState("");

  if (!state) {
    return <p className="loading">Waiting for fleet data…</p>;
  }

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
              <div className="ship-card" key={ship.shipId}>
                <img
                  className="ship-badge"
                  src={shipImageUrl(ship.shipType ?? "unknown", ship.shipTypeDisplay ?? ship.shipType, spec?.sizeClass, ship.name)}
                  alt={ship.shipTypeDisplay ?? ship.shipType ?? "Unknown ship"}
                />
                <div className="ship-card-body">
                  <div className="ship-card-name">
                    {ship.name || ship.shipTypeDisplay || ship.shipType}
                    {ship.location === "Current" ? <span className="chip legal-clean ship-active-chip">active</span> : null}
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
              </div>
            );
          })}
        </div>
      </section>

      <section className="card wide">
        <h2>Current ship loadout</h2>
        {state.currentShipModules.length === 0 ? (
          <p className="empty-hint">No module data yet - it's populated from the game's Loadout event.</p>
        ) : (
          state.currentShipModules.map((m, i) => (
            <div className="card-row" key={`${m.slot}-${i}`}>
              <span className="label">
                {m.slot}
                {!m.on ? <span className="chip legal-caution module-off-chip">off</span> : null}
              </span>
              <span className="value">
                {moduleDisplayName(moduleSpecs, m.item)}
                {m.engineeringBlueprint ? (
                  <span className="txn-time"> · {m.engineeringBlueprint} G{m.engineeringLevel}</span>
                ) : null}
              </span>
            </div>
          ))
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
