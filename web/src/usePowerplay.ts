import { useEffect, useState } from "react";
import { apiBaseUrl, type PowerplayHistoryPoint, type PowerplayStanding } from "./api";

export function usePowerplay() {
  const [standing, setStanding] = useState<PowerplayStanding | null>(null);
  const [history, setHistory] = useState<PowerplayHistoryPoint[]>([]);

  useEffect(() => {
    fetch(`${apiBaseUrl}/api/powerplay/standing`)
      .then((r) => r.json())
      .then(setStanding)
      .catch(() => {});

    fetch(`${apiBaseUrl}/api/powerplay/history`)
      .then((r) => r.json())
      .then(setHistory)
      .catch(() => {});
  }, []);

  return { standing, history };
}
