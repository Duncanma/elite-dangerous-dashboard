import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useEconomics } from "../useEconomics";
import type { Transaction, TransactionCategory } from "../api";
import { CHART_RANGES, filterAndGroup, formatHistorySpan, rangeShowsFullHistory, usePersistedChartRange } from "../chartRange";
import { summarizeTransactions } from "../economicsSummary";
import { formatCredits } from "../shipLabels";

const CATEGORY_LABELS: Record<TransactionCategory, string> = {
  TradeBuy: "Trade (buy)",
  TradeSell: "Trade (sell)",
  BountyVoucher: "Bounty",
  BondVoucher: "Combat bond",
  ExplorationVoucher: "Exploration data",
  MissionReward: "Mission reward",
  Repair: "Repair",
  Refuel: "Refuel",
  ShipyardBuy: "Ship purchase",
  ShipyardSell: "Ship sale",
  Rearm: "Ammo/drones",
  Rebuy: "Ship rebuy",
  SearchAndRescue: "Search and rescue",
  CrewWages: "Crew wage",
  Other: "Other",
};

function formatAxisCredits(amount: number): string {
  if (Math.abs(amount) >= 1_000_000) return `${(amount / 1_000_000).toFixed(0)}M`;
  if (Math.abs(amount) >= 1_000) return `${(amount / 1_000).toFixed(0)}K`;
  return `${amount}`;
}

export default function EconomicsPage() {
  const { transactions, summary } = useEconomics();
  const [range, setRange] = usePersistedChartRange("economics-chart-range");

  const fullChartData = summary.map((p) => ({
    time: new Date(p.timestampUtc).getTime(),
    balance: p.balance,
  }));
  const chartData = filterAndGroup(fullChartData, range);
  const txnSummary = summarizeTransactions(transactions);

  return (
    <div className="grid">
      <section className="card wide">
        <h2>Funds over time</h2>
        <div className="range-selector">
          {CHART_RANGES.map((r) => {
            const redundant = rangeShowsFullHistory(fullChartData, r.value);
            return (
              <button
                key={r.value}
                className={`range-button ${range === r.value ? "active" : ""} ${redundant ? "redundant" : ""}`}
                onClick={() => setRange(r.value)}
                title={redundant ? "Same as All time - not enough history recorded yet" : undefined}
              >
                {r.label}
              </button>
            );
          })}
        </div>
        {fullChartData.length >= 2 && CHART_RANGES.some((r) => rangeShowsFullHistory(fullChartData, r.value)) ? (
          <p className="empty-hint range-hint">
            Only {formatHistorySpan(fullChartData)} of history recorded so far - narrower ranges will differ once there's more.
          </p>
        ) : null}
        {fullChartData.length < 2 ? (
          <p className="empty-hint">Not enough transaction history yet to chart a trend.</p>
        ) : chartData.length < 2 ? (
          <p className="empty-hint">No transactions in this range - try a wider one.</p>
        ) : (
          <div style={{ width: "100%", height: 280 }}>
            <ResponsiveContainer>
              <LineChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                <XAxis
                  dataKey="time"
                  type="number"
                  domain={["dataMin", "dataMax"]}
                  tickFormatter={(t) => new Date(t).toLocaleDateString()}
                  fontSize={12}
                />
                <YAxis tickFormatter={formatAxisCredits} fontSize={12} width={56} />
                <Tooltip
                  labelFormatter={(t) => new Date(t as number).toLocaleString()}
                  formatter={(value) => [formatCredits(Number(value), { signed: true }), "Balance"]}
                />
                <Line type="monotone" dataKey="balance" stroke="var(--accent)" dot={false} strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </section>

      <section className="card wide">
        <h2>Transactions</h2>
        {transactions.length === 0 ? (
          <p className="empty-hint">No transactions recorded yet.</p>
        ) : (
          <>
            <p className="empty-hint txn-summary-caption">Summary of the {transactions.length} transactions below</p>
            <div className="txn-summary">
              <div className="txn-summary-col">
                <h3>Income</h3>
                {txnSummary.income.length === 0 ? (
                  <p className="empty-hint">None</p>
                ) : (
                  txnSummary.income.map((line) => (
                    <div className="card-row" key={line.label}>
                      <span className="label">{line.label}</span>
                      <span className="value amount-positive">{formatCredits(line.amount, { signed: true })}</span>
                    </div>
                  ))
                )}
              </div>
              <div className="txn-summary-col">
                <h3>Expenses</h3>
                {txnSummary.expenses.length === 0 ? (
                  <p className="empty-hint">None</p>
                ) : (
                  txnSummary.expenses.map((line) => (
                    <div className="card-row" key={line.label}>
                      <span className="label">{line.label}</span>
                      <span className="value amount-negative">{formatCredits(line.amount, { signed: true })}</span>
                    </div>
                  ))
                )}
              </div>
            </div>
            <div className="card-row txn-summary-net">
              <span className="label">Net</span>
              <span className={`value ${txnSummary.net >= 0 ? "amount-positive" : "amount-negative"}`}>
                {formatCredits(txnSummary.net, { signed: true })}
              </span>
            </div>
            {transactions.map((t: Transaction) => (
              <div className="card-row" key={t.id}>
                <span className="label">
                  {CATEGORY_LABELS[t.category]}
                  {t.description ? ` — ${t.description}` : ""}
                  <span className="txn-time"> · {new Date(t.timestampUtc).toLocaleString()}</span>
                </span>
                <span className={`value ${t.amount >= 0 ? "amount-positive" : "amount-negative"}`}>
                  {formatCredits(t.amount, { signed: true })}
                </span>
              </div>
            ))}
          </>
        )}
      </section>
    </div>
  );
}
