// Only the station types EDAssets has icons for - everything else (planetary
// types, FleetCarrier, SpaceConstructionDepot, ...) simply gets no icon.
const STATION_ICON_FILES: Record<string, string> = {
  Coriolis: "coriolis",
  Ocellus: "ocellus",
  Orbis: "orbis",
  Outpost: "outpost",
  AsteroidBase: "asteroidbase",
  MegaShip: "megaship",
};

export function stationIconUrl(stationType: string | null | undefined): string | null {
  if (!stationType) return null;
  const file = STATION_ICON_FILES[stationType];
  return file ? `/stations/${file}.svg` : null;
}
