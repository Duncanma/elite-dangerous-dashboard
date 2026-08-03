// Only Federation and Empire have their own rank track (RankStanding) - Alliance has no
// equivalent, so there's nothing to key an icon off for it here.
const FACTION_ICON_FILES: Record<string, string> = {
  federation: "federation",
  empire: "empire",
};

export function factionIconUrl(faction: string): string | null {
  const file = FACTION_ICON_FILES[faction];
  return file ? `/factions/${file}.svg` : null;
}
