import { useEffect, useState } from "react";
import { apiBaseUrl, type BalancePoint, type CategoryTotal, type Transaction } from "./api";
import { ensureStarted, getHub } from "./signalr";
import { rangeSinceUtc, type ChartRange } from "./chartRange";

function withSince(path: string, range: ChartRange, extra: Record<string, string> = {}): string {
  const params = new URLSearchParams(extra);
  const since = rangeSinceUtc(range);
  if (since) {
    params.set("since", since);
  }

  const qs = params.toString();
  return qs ? `${path}?${qs}` : path;
}

// `range` scopes both the transaction feed and the income/expense/net summary
// to the same window as the funds-over-time chart above them on the
// Economics page (see issue #3) - refetches whenever it changes.
export function useEconomics(range: ChartRange) {
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [summary, setSummary] = useState<BalancePoint[]>([]);
  const [categoryTotals, setCategoryTotals] = useState<CategoryTotal[]>([]);

  const refetchSummaries = () => {
    fetch(`${apiBaseUrl}/api/economics/summary`)
      .then((r) => r.json())
      .then(setSummary)
      .catch(() => {});

    fetch(`${apiBaseUrl}${withSince("/api/economics/transaction-summary", range)}`)
      .then((r) => r.json())
      .then(setCategoryTotals)
      .catch(() => {});
  };

  const refetch = () => {
    fetch(`${apiBaseUrl}${withSince("/api/economics/transactions", range, { limit: "200" })}`)
      .then((r) => r.json())
      .then(setTransactions)
      .catch(() => {});

    refetchSummaries();
  };

  useEffect(() => {
    refetch();

    const hub = getHub();
    const onTransactionAdded = (record: Transaction) => {
      // A single new transaction doesn't warrant a full refetch of the chart
      // series (which needs the recomputed running balance) - just prepend
      // to the list live (it's always "now", so always within the current
      // range) and refetch the two aggregates in the background.
      setTransactions((prev) => [record, ...prev].slice(0, 200));
      refetchSummaries();
    };

    hub.on("TransactionAdded", onTransactionAdded);
    ensureStarted().catch(() => {});

    return () => {
      hub.off("TransactionAdded", onTransactionAdded);
    };
  }, [range]);

  return { transactions, summary, categoryTotals };
}
