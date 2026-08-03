import { useEffect, useState } from "react";
import { apiBaseUrl, type ColonisationSite, type ShippingPlanResult } from "./api";

export function useColonisation() {
  const [sites, setSites] = useState<ColonisationSite[]>([]);

  useEffect(() => {
    fetch(`${apiBaseUrl}/api/systems/sites`)
      .then((r) => r.json())
      .then(setSites)
      .catch(() => {});
  }, []);

  const generateShippingPlan = async (siteId: number): Promise<ShippingPlanResult> => {
    const response = await fetch(`${apiBaseUrl}/api/systems/sites/${siteId}/shipping-plan`, { method: "POST" });
    return response.json();
  };

  return { sites, generateShippingPlan };
}
