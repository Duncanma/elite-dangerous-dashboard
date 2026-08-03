import type { Transaction, TransactionCategory } from "./api";

export interface SummaryLine {
  label: string;
  amount: number;
}

export interface TransactionSummary {
  income: SummaryLine[];
  expenses: SummaryLine[];
  net: number;
}

// TradeBuy/TradeSell are netted into one "Trade profit" line rather than
// shown separately - matches how the rest of the app already treats trading
// profit as a derived, computed-at-query-time figure rather than its own
// stored category (see Transaction.cs).
function bucketLabel(category: TransactionCategory): string {
  switch (category) {
    case "TradeBuy":
    case "TradeSell":
      return "Trade profit";
    case "BountyVoucher":
      return "Bounty";
    case "BondVoucher":
      return "Combat bonds";
    case "ExplorationVoucher":
      return "Exploration data";
    case "MissionReward":
      return "Mission rewards";
    case "ShipyardSell":
      return "Ship sales";
    case "ShipyardBuy":
      return "Ship purchases";
    case "Repair":
      return "Repairs";
    case "Refuel":
      return "Refuelling";
    case "Other":
      return "Other";
  }
}

export function summarizeTransactions(transactions: Transaction[]): TransactionSummary {
  const totals = new Map<string, number>();
  for (const t of transactions) {
    const label = bucketLabel(t.category);
    totals.set(label, (totals.get(label) ?? 0) + t.amount);
  }

  const income: SummaryLine[] = [];
  const expenses: SummaryLine[] = [];
  let net = 0;
  for (const [label, amount] of totals) {
    net += amount;
    if (amount === 0) continue;
    (amount > 0 ? income : expenses).push({ label, amount });
  }

  income.sort((a, b) => b.amount - a.amount);
  expenses.sort((a, b) => a.amount - b.amount);

  return { income, expenses, net };
}
