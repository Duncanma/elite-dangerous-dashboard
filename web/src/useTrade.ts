import { useEffect, useState } from "react";
import {
  apiBaseUrl,
  type EddnStatus,
  type TradeRouteCandidate,
  type TradeRoutePlan,
  type TradeSearchCriteria,
  type TradeSearchDefaults,
  type TradeSystemCoverage,
} from "./api";
import { ensureStarted, getHub } from "./signalr";

export function useTrade() {
  const [status, setStatus] = useState<EddnStatus | null>(null);
  const [activePlan, setActivePlan] = useState<TradeRoutePlan | null>(null);
  const [candidates, setCandidates] = useState<TradeRouteCandidate[]>([]);
  const [defaults, setDefaults] = useState<TradeSearchDefaults | null>(null);
  const [coverage, setCoverage] = useState<TradeSystemCoverage | null>(null);
  const [searching, setSearching] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refetchActive = () => {
    fetch(`${apiBaseUrl}/api/trade/active`)
      .then((r) => r.json())
      .then(setActivePlan)
      .catch(() => {});
  };

  const refetchStatus = () => {
    fetch(`${apiBaseUrl}/api/trade/status`)
      .then((r) => r.json())
      .then(setStatus)
      .catch(() => {});
  };

  const refetchCoverage = () => {
    fetch(`${apiBaseUrl}/api/trade/coverage`)
      .then((r) => r.json())
      .then(setCoverage)
      .catch(() => {});
  };

  useEffect(() => {
    refetchActive();
    refetchStatus();
    refetchCoverage();
    fetch(`${apiBaseUrl}/api/trade/defaults`)
      .then((r) => r.json())
      .then(setDefaults)
      .catch(() => {});
    const statusInterval = setInterval(refetchStatus, 10_000);

    const hub = getHub();
    const onProgress = () => refetchActive();
    hub.on("TradeRouteProgressChanged", onProgress);
    ensureStarted().catch(() => {});

    return () => {
      clearInterval(statusInterval);
      hub.off("TradeRouteProgressChanged", onProgress);
    };
  }, []);

  const search = async (criteria: TradeSearchCriteria) => {
    setSearching(true);
    setError(null);
    try {
      const res = await fetch(`${apiBaseUrl}/api/trade/search`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(criteria),
      });
      setCandidates(await res.json());
      refetchCoverage(); // a search may have resolved new systems' coordinates via EDSM
    } finally {
      setSearching(false);
    }
  };

  const activate = async (criteria: TradeSearchCriteria) => {
    setError(null);
    const res = await fetch(`${apiBaseUrl}/api/trade/activate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(criteria),
    });

    if (!res.ok) {
      const body = await res.json().catch(() => null);
      setError(body?.error ?? "Could not activate a route.");
      return;
    }

    setActivePlan(await res.json());
    setCandidates([]);
  };

  const abandon = async () => {
    await fetch(`${apiBaseUrl}/api/trade/abandon`, { method: "POST" });
    setActivePlan(null);
  };

  return { status, activePlan, candidates, defaults, coverage, searching, error, search, activate, abandon };
}
