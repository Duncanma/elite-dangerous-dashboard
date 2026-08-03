import { useEffect, useState } from "react";
import { apiBaseUrl, type DashboardState } from "./api";
import { ensureStarted, getHub } from "./signalr";

export function useDashboardState() {
  const [state, setState] = useState<DashboardState | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const hub = getHub();

    fetch(`${apiBaseUrl}/api/state`)
      .then((r) => r.json())
      .then((data: DashboardState) => {
        if (!cancelled) setState(data);
      })
      .catch(() => {
        /* SignalR reconnect/initial retry will eventually recover state */
      });

    const onChanged = (updated: DashboardState) => setState(updated);
    hub.on("DashboardStateChanged", onChanged);

    const onReconnected = () => setConnected(true);
    const onClose = () => setConnected(false);
    hub.onreconnected(onReconnected);
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
