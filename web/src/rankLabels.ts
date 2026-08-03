// Tier names from Frontier's own journal manual appendix (index 0 = lowest).
const COMBAT = ["Harmless", "Mostly Harmless", "Novice", "Competent", "Expert", "Master", "Dangerous", "Deadly", "Elite"];
const TRADE = ["Penniless", "Mostly Penniless", "Peddler", "Dealer", "Merchant", "Broker", "Entrepreneur", "Tycoon", "Elite"];
const EXPLORE = ["Aimless", "Mostly Aimless", "Scout", "Surveyor", "Explorer", "Pathfinder", "Ranger", "Pioneer", "Elite"];
const CQC = ["Helpless", "Mostly Helpless", "Amateur", "Semi Professional", "Professional", "Champion", "Hero", "Legend", "Elite"];
const FEDERATION = [
  "None", "Recruit", "Cadet", "Midshipman", "Petty Officer", "Chief Petty Officer", "Warrant Officer",
  "Ensign", "Lieutenant", "Lt. Commander", "Post Commander", "Post Captain", "Rear Admiral", "Vice Admiral", "Admiral",
];
const EMPIRE = [
  "None", "Outsider", "Serf", "Master", "Squire", "Knight", "Lord",
  "Baron", "Viscount", "Count", "Earl", "Marquis", "Duke", "Prince", "King",
];

function label(tiers: string[], index: number): string {
  if (index < tiers.length) {
    return tiers[index];
  }
  // Odyssey's Elite I-V prestige tiers extend past the base list for Combat/Trade/Explore/CQC.
  return `Elite ${"I".repeat(index - tiers.length + 1)}`;
}

export const rankLabel = {
  combat: (index: number) => label(COMBAT, index),
  trade: (index: number) => label(TRADE, index),
  explore: (index: number) => label(EXPLORE, index),
  cqc: (index: number) => label(CQC, index),
  federation: (index: number) => label(FEDERATION, index),
  empire: (index: number) => label(EMPIRE, index),
};
