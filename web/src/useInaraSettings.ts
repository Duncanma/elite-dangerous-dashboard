import { useEffect, useState } from "react";
import { apiBaseUrl } from "./api";

export interface InaraSettings {
  hasApiKey: boolean;
  lastSyncStatus: string | null;
  lastSyncAtUtc: string | null;
}

export function useInaraSettings() {
  const [settings, setSettings] = useState<InaraSettings | null>(null);

  const refetch = () => {
    fetch(`${apiBaseUrl}/api/settings/inara`)
      .then((r) => r.json())
      .then(setSettings)
      .catch(() => {});
  };

  useEffect(refetch, []);

  const save = async (apiKey: string) => {
    await fetch(`${apiBaseUrl}/api/settings/inara`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ apiKey }),
    });
    refetch();
  };

  const clear = async () => {
    await fetch(`${apiBaseUrl}/api/settings/inara`, { method: "DELETE" });
    refetch();
  };

  return { settings, save, clear, refetch };
}
