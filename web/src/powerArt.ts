// Journal's Power field spells some names differently from EDAssets' filenames
// (notably "A. Lavigny-Duval" vs "arissa-lavigny-duval") - normalization can't
// bridge that, so this is an explicit table of the known Powerplay powers.
const POWER_ICON_FILES: Record<string, string> = {
  "A. Lavigny-Duval": "arissa-lavigny-duval",
  "Aisling Duval": "aisling-duval",
  "Archon Delaine": "archon-delaine",
  "Denton Patreus": "denton-patreus",
  "Edmund Mahon": "edmund-mahon",
  "Felicia Winters": "felicia-winters",
  "Li Yong-Rui": "li-yong-rui",
  "Pranav Antal": "pranav-antal",
  "Yuri Grom": "yuri-grom",
  "Zachary Hudson": "zachary-hudson",
  "Zemina Torval": "zemina-torval",
};

/** Null for an unpledged commander or a power without a bundled icon. */
export function powerIconUrl(power: string | null | undefined): string | null {
  if (!power) return null;
  const file = POWER_ICON_FILES[power];
  return file ? `/powers/${file}.svg` : null;
}
