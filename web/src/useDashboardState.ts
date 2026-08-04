import { useEffect, useState } from "react";
import { apiBaseUrl, type DashboardState } from "./api";
import { ensureStarted, getHub } from "./signalr";

export function useDashboardState() {
  const [state, setState] = useState<DashboardState | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const hub = getHub();

    const loadState = () =>
      fetch(`${apiBaseUrl}/api/state`)
        .then((r) => r.json())
        .then((data: DashboardState) => {
          if (!cancelled) setState(data);
        })
        .catch(() => {
          /* SignalR reconnect/initial retry will eventually recover state */
        });

    loadState();

    const onChanged = (updated: DashboardState) => setState(updated);
    hub.on("DashboardStateChanged", onChanged);

    const onReconnected = () => {
      setConnected(true);
      // Pushes broadcast during the drop are never re-sent - resync from REST.
      loadState();
    };
    const onReconnecting = () => setConnected(false);
    const onClose = () => setConnected(false);
    hub.onreconnected(onReconnected);
    hub.onreconnecting(onReconnecting);
    hub.onclose(onClose);

    ensureStarted()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => {
      cancelled = true;
      hub.off("DashboardStateChanged", onChanged);
    };
  }, []);

  return { state, connected };
}
