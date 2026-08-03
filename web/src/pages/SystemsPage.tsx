import { useState } from "react";
import { useColonisation } from "../useColonisation";
import type { ColonisationSite, ShippingPlanResult } from "../api";
import { stationIconUrl } from "../stationArt";
import { MaskIcon } from "../MaskIcon";

function statusChip(site: ColonisationSite): { label: string; cls: string } {
  if (site.constructionFailed) return { label: "Failed", cls: "legal-wanted" };
  if (site.constructionComplete) return { label: "Complete", cls: "legal-clean" };
  return { label: "In progress", cls: "legal-caution" };
}

function stationTypeLabel(stationType: string | null): string {
  if (!stationType) return "";
  if (["SurfaceStation", "CraterPort", "CraterOutpost", "OnFootSettlement"].includes(stationType)) {
    return "planetary";
  }
  return "orbital";
}

export default function SystemsPage() {
  const { sites, generateShippingPlan } = useColonisation();
  const [plans, setPlans] = useState<Record<number, ShippingPlanResult>>({});
  const [loadingSiteId, setLoadingSiteId] = useState<number | null>(null);

  const handleGeneratePlan = async (siteId: number) => {
    setLoadingSiteId(siteId);
    try {
      const plan = await generateShippingPlan(siteId);
      setPlans((prev) => ({ ...prev, [siteId]: plan }));
    } finally {
      setLoadingSiteId(null);
    }
  };

  return (
    <div className="grid">
      <section className="card wide">
        <img src="/branding/colonia-expansion-initiative.png" alt="Colonia Expansion Initiative" className="cei-banner" />
        <h2>My Systems ({sites.length})</h2>
        {sites.length === 0 ? (
          <p className="empty-hint">No colonisation construction sites tracked yet.</p>
        ) : null}
      </section>

      {sites.map((site) => {
        const status = statusChip(site);
        const outstanding = site.resources.filter((r) => r.providedAmount < r.requiredAmount);
        const plan = plans[site.id];
        return (
          <section className="card wide" key={site.id}>
            <h2>
              {site.stationName} — {site.systemName}
            </h2>
            <div className="card-row">
              <span className="label">Status</span>
              <span className={`value chip ${status.cls}`}>{status.label}</span>
            </div>
            <div className="progress-track">
              <div className="progress-fill" style={{ width: `${Math.round(site.constructionProgress * 100)}%` }} />
            </div>
            <div className="card-row muted">
              <span className="label">Progress</span>
              <span className="value">{Math.round(site.constructionProgress * 100)}%</span>
            </div>

            {!site.constructionComplete && outstanding.length > 0 ? (
              <>
                <div className="card-row muted resource-header">
                  <span className="label">Resources still needed ({outstanding.length})</span>
                </div>
                {outstanding.map((r) => (
                  <div className="card-row" key={r.commodityName}>
                    <span className="label">{r.commodityDisplayName ?? r.commodityName}</span>
                    <span className="value">
                      {r.providedAmount.toLocaleString()} / {r.requiredAmount.toLocaleString()}
                    </span>
                  </div>
                ))}

                <button
                  className="primary-button"
                  disabled={loadingSiteId === site.id}
                  onClick={() => handleGeneratePlan(site.id)}
                >
                  {loadingSiteId === site.id ? "Finding suppliers…" : "Generate shipping plan"}
                </button>

                {plan ? (
                  plan.success ? (
                    <div className="shipping-plan">
                      {plan.stops.map((stop, i) => (
                        <div className="shipping-plan-stop" key={`${stop.stationName}-${i}`}>
                          <div className="shipping-plan-stop-header">
                            <span className="shipping-plan-stop-index">{i + 1}</span>
                            <div>
                              <div className="shipping-plan-stop-name">
                                {stationIconUrl(stop.stationType) ? (
                                  <MaskIcon
                                    src={stationIconUrl(stop.stationType)!}
                                    className="inline-icon"
                                    label={stop.stationType ?? undefined}
                                  />
                                ) : null}
                                {stop.stationName}
                                {stop.stationType ? (
                                  <span className="txn-time"> · {stationTypeLabel(stop.stationType)}</span>
                                ) : null}
                              </div>
                              <div className="txn-time">
                                {stop.systemName} · {stop.distanceLy.toLocaleString()} ly
                              </div>
                            </div>
                          </div>
                          {stop.items.map((item) => (
                            <div className="card-row" key={item.commodityName}>
                              <span className="label">{item.commodityName}</span>
                              <span className="value">{item.quantity.toLocaleString()}</span>
                            </div>
                          ))}
                        </div>
                      ))}
                      {plan.unresolvedCommodities.length > 0 ? (
                        <p className="empty-hint">
                          No current supplier found for: {plan.unresolvedCommodities.join(", ")}
                        </p>
                      ) : null}
                    </div>
                  ) : (
                    <p className="empty-hint">{plan.message}</p>
                  )
                ) : null}
              </>
            ) : null}
          </section>
        );
      })}
    </div>
  );
}
