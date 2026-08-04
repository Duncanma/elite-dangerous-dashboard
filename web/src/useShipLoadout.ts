import { useEffect, useState } from "react";
import { apiBaseUrl, type ShipLoadoutDetail } from "./api";

// Loads the persisted last-seen loadout snapshot for one ship. A 404 means
// none was ever recorded for it - the journal only emits per-module detail
// for the ship the commander is sitting in, so stored ships get a snapshot
// only after being boarded.
export function useShipLoadout(shipId: number | null) {
  const [loadout, setLoadout] = useState<ShipLoadoutDetail | null>(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    if (shipId === null) {
      setLoadout(null);
      setNotFound(false);
      return;
    }

    let cancelled = false;
    setLoadout(null);
    setNotFound(false);

    fetch(`${apiBaseUrl}/api/fleet/${shipId}/loadout`)
      .then(async (r) => {
        if (r.status === 404) {
          if (!cancelled) setNotFound(true);
          return;
        }
        const data: ShipLoadoutDetail = await r.json();
        if (!cancelled) setLoadout(data);
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
  }, [shipId]);

  return { loadout, notFound };
}
