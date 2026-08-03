import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { usePowerplay } from "../usePowerplay";
import { CHART_RANGES, filterAndGroup, formatHistorySpan, rangeShowsFullHistory, usePersistedChartRange } from "../chartRange";
import { powerIconUrl } from "../powerArt";
import { MaskIcon } from "../MaskIcon";

function formatDuration(seconds: number): string {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  return `${days}d ${hours}h`;
}

export default function PowerplayPage() {
  const { standing, history } = usePowerplay();
  const [range, setRange] = usePersistedChartRange("powerplay-chart-range");

  const fullChartData = history.map((h) => ({
    time: new Date(h.timestampUtc).getTime(),
    merits: h.totalMerits,
  }));
  const chartData = filterAndGroup(fullChartData, range);

  if (!standing) {
    return (
      <div className="grid">
        <section className="card wide">
          <h2>Powerplay</h2>
          <p className="empty-hint">Not currently pledged to a power.</p>
        </section>
      </div>
    );
  }

  const iconUrl = powerIconUrl(standing.power);

  return (
    <div className="grid">
      <section className="card">
        <h2>Standing</h2>
        <div className="ship-portrait">
          {iconUrl ? (
            <div className="power-badge">
              <MaskIcon src={iconUrl} className="power-badge-icon" label={standing.power} />
            </div>
          ) : null}
          <div>
            <div className="ship-portrait-name">{standing.power}</div>
            <div className="ship-portrait-type">Rank {standing.rank}</div>
          </div>
        </div>
        <div className="card-row">
          <span className="label">Total merits</span>
          <span className="value">{standing.totalMerits.toLocaleString()}</span>
        </div>
        <div className="card-row muted">
          <span className="label">Time pledged</span>
          <span className="value">{formatDuration(standing.timePledgedSeconds)}</span>
        </div>
      </section>

      <section className="card wide">
        <h2>Merits over time</h2>
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
          <p className="empty-hint">Not enough history yet to chart a trend.</p>
        ) : chartData.length < 2 ? (
          <p className="empty-hint">No history in this range - try a wider one.</p>
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
                <YAxis fontSize={12} width={56} />
                <Tooltip
                  labelFormatter={(t) => new Date(t as number).toLocaleString()}
                  formatter={(value) => [Number(value).toLocaleString(), "Merits"]}
                />
                <Line type="monotone" dataKey="merits" stroke="var(--accent)" dot={false} strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </section>
    </div>
  );
}
