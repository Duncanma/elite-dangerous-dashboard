# Elite Dangerous Dashboard

A personal, real-time dashboard for Elite Dangerous. It runs on your gaming PC, reads your Journal files as the game writes them, and serves a live web dashboard to every device on your home network — phone, tablet, laptop — with no manual refresh.

Views: **Dashboard** (commander, current ship portrait, rank progress, location, fleet summary, active trade route, economic snapshot with a spend breakdown), **Economics** (funds-over-time chart with 30-day/90-day/all-time views, categorized transaction feed with an income/expense summary), **Fleet** (owned ships with real coriolis-derived specs and human-readable module loadout names, wishlist, upgrade plans), **Trade** (route planner backed by a live EDDN price feed, with progress tracking, and a settings panel to constrain searches by jump radius, pad size, hop distance, and planet/fleet-carrier exclusion - all defaulted from your current ship and location), **My Systems** (colonisation sites you're building or contributing to, with a shipping-plan generator for outstanding materials), **Powerplay** (personal standing and merits history with the same time-range views as Economics), and **Settings** (Inara sync).

## How it works, in short

One ASP.NET Core process tails your Journal folder, reconciles events into a SQLite database and an in-memory state store, and pushes updates to connected browsers over SignalR. A React SPA renders that state and is served from the same process on the same port, so there's nothing else to run in production. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full design.

The first time the app runs against a given database, it also does a one-time import of every *other* Journal file it finds (not just the current session) so Economics/Powerplay history goes back as far as your actual journal files do, not just to whenever you first ran the app. This can take up to a minute or so depending on how much journal history you have; every run after that is fast, since it only replays the current session.

## Prerequisites

- Windows (the Saved Games/Journal path resolution and the tray app are Windows-specific)
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) (for building the frontend)
- Elite Dangerous installed and having been launched at least once (so `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous` exists)

## Running in development

Two processes, run side by side:

```bash
dotnet run --project src/EliteDashboard.Host
```

```bash
cd web
npm install
npm run dev
```

Open the URL Vite prints (`http://localhost:5173`). The frontend always calls the API/SignalR hub on port 5080 of whatever host served the page (see `apiBaseUrl` in `web/src/api.ts`) — no proxy config needed. The Host's dev launch profile binds `0.0.0.0:5080`, so both the Vite dev server and the Host are reachable from other LAN devices while you're developing. In dev, `wwwroot` is empty and the Host's static-file middleware is a no-op — only the Vite dev server serves the SPA.

The Host reads your real Journal folder by default. To point it at a different folder (a fixture, a copy, a replay scratch directory) instead of your live journal, set:

```json
{ "Journal": { "JournalPathOverride": "C:\\path\\to\\journal\\folder" } }
```

in `src/EliteDashboard.Host/appsettings.Development.json`, or via the `Journal__JournalPathOverride` environment variable.

## Installing for everyday use

For a normal "always on, starts when I log in, reachable from my phone" setup, use the scripts in [deploy/](deploy/README.md):

```powershell
./deploy/publish.ps1
```

Builds the frontend, copies it into the Host's `wwwroot`, and publishes both `EliteDashboard.Host` and `EliteDashboard.Tray` to `publish/`. Re-run this after pulling any update.

```powershell
./deploy/install.ps1   # run as Administrator
```

Registers a Scheduled Task that launches the tray app at logon and opens a Windows Firewall rule (Private profile only) so other devices on your LAN can reach port `5080`. This is a separate, deliberate step from publishing since it changes standing configuration on your PC — you don't have to run it at all; double-clicking `EliteDashboard.Tray.exe` from the publish folder works fine for an on-demand session.

Once running, right-click the tray icon for a menu: open the dashboard in your browser, show a QR code + LAN URL for connecting a phone, restart the backend, or view logs.

To remove the Scheduled Task and firewall rule later: `./deploy/uninstall.ps1` (run as Administrator). It doesn't touch your app files, database, or journal data.

## Data storage

Everything the app persists lives under `%LOCALAPPDATA%\EliteDashboard\`:

- `dashboard.db` — SQLite database (transactions, fleet/wishlist data, cached trade/market data, colonisation and Powerplay history, ship/module specs, star system coordinates, settings)
- `logs\host.log` — the Host's console output, captured by the tray app
- `ship-art\` — your own ship images (see below)

This is independent of wherever you published the app binaries, so republishing or moving the install directory never loses your data.

## Custom ship art

The Fleet page and Dashboard ship portrait can show real images instead of the generated placeholder badge. Nothing ships with the app by default (no legal, stable source of ship art was found - see [ARCHITECTURE.md](docs/ARCHITECTURE.md) for what was tried) and nothing you add is ever checked into the repo - it's personal to your install, so drop your own image files into:

```
%LOCALAPPDATA%\EliteDashboard\ship-art\by-type\{Ship type name}.webp    - generic art for any ship of that type
%LOCALAPPDATA%\EliteDashboard\ship-art\by-ship\{Your ship's name}.webp  - a specific hull (your paint job, your own screenshot)
```

Use the ship type name exactly as shown on the Fleet page (e.g. `Krait Mk II.webp`, `Panther Clipper Mk II.png`) or your ship's given name (e.g. `Hunter.webp`) for a specific hull - matching ignores spacing, punctuation, and case, so `krait-mk-ii.webp` works just as well. `.webp`, `.png`, `.jpg`, and `.jpeg` are all supported. A `by-ship` match wins over `by-type` if both exist for the same ship; anything with neither falls back to the generated placeholder. Files are picked up immediately - no restart needed.

## Inara sync

If you have an [Inara](https://inara.cz/) account, open the **Settings** tab in the dashboard and paste in your personal API key (Inara account → Referral & API section). Once configured, the app pushes your credits, combat/trade/exploration/CQC ranks, Powerplay standing, and lifetime statistics to Inara every 10 minutes. The key is stored only in your local `dashboard.db`, never anywhere else. Remove it from the same Settings page at any time to stop syncing.

## Troubleshooting

- **"Waiting for data…" forever**: check `logs\host.log` (or the console, in dev) for `Starting journal tailer against <path>` — if that path doesn't exist, Elite Dangerous hasn't created a journal folder yet (launch the game once), or your `JournalPathOverride` is wrong.
- **Can't reach the dashboard from another device**: confirm `install.ps1` was run (for the firewall rule), and that both devices are on the same network. The tray's QR code shows the exact URL to use.
- **Rebuilding fails with a file-in-use error (`MSB3021`/`MSB3027`)**: a previous `EliteDashboard.Host.exe` or `EliteDashboard.Tray.exe` is still running and holding the binary. Stop it (Task Manager, or the tray icon's Exit) before rebuilding.
- **Ship specs missing for a ship**: coriolis-data doesn't catalogue every ship yet (notably the newest releases) — see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#reference-data-import) for how that import works.
- **Ship shows a colored placeholder badge instead of a real image**: expected until you supply your own — see [Custom ship art](#custom-ship-art) above.
- **The app doesn't reflect a change after republishing**: it's installed as a PWA, and the service worker caches the app shell (the JS/CSS bundle) for offline installability — a normal browser refresh can still serve the old cached version. Hard-refresh (Ctrl+Shift+R) or clear the site's cached data once after updating.
- **First launch is noticeably slower than usual**: expected the very first time the app runs against a given database — it's doing the one-time historical import described above. Check the console/log for `Backfilling N historical journal files` — once you see `Historical journal backfill complete`, every subsequent launch is back to normal speed.
- **A colonisation site you're building isn't showing up in "My Systems" yet**: it appears once you've either delivered at least one load of cargo there, or claimed that system for colonisation yourself (the in-game action that makes you its "architect") — just docking at a site (e.g. passing through, or visiting someone else's project) doesn't add it, on purpose.

## Engineering documentation

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for project structure, data flow, the historical backfill and replay-safety rules, the colonisation shipping planner, how to add a new journal event, migrations, and known gaps.

## Attribution

Elite Dangerous Dashboard was created using assets and imagery from Elite Dangerous, with the permission of Frontier Developments plc, for non-commercial purposes. It is not endorsed by nor reflects the views or opinions of Frontier Developments and no employee of Frontier Developments was involved in the making of it.

Power and station icons courtesy of [edassets.org](https://edassets.org/), a fan-maintained repository of Elite Dangerous assets.
