import { useEffect, useState } from "react";
import { apiBaseUrl, type BalancePoint, type Transaction } from "./api";
import { ensureStarted, getHub } from "./signalr";

export function useEconomics() {
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [summary, setSummary] = useState<BalancePoint[]>([]);

  const refetch = () => {
    fetch(`${apiBaseUrl}/api/economics/transactions?limit=200`)
      .then((r) => r.json())
      .then(setTransactions)
      .catch(() => {});

    fetch(`${apiBaseUrl}/api/economics/summary`)
      .then((r) => r.json())
      .then(setSummary)
      .catch(() => {});
  };

  useEffect(() => {
    refetch();

    const hub = getHub();
    const onTransactionAdded = (record: Transaction) => {
      // A single new transaction doesn't warrant a full refetch of the chart
      // series (which needs the recomputed running balance) - just prepend
      // to the list live, and refetch the summary in the background.
      setTransactions((prev) => [record, ...prev].slice(0, 200));
      fetch(`${apiBaseUrl}/api/economics/summary`)
        .then((r) => r.json())
        .then(setSummary)
        .catch(() => {});
    };

    hub.on("TransactionAdded", onTransactionAdded);
    ensureStarted().catch(() => {});

    return () => {
      hub.off("TransactionAdded", onTransactionAdded);
    };
  }, []);

  return { transactions, summary };
}
