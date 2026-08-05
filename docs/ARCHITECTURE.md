# Architecture

Engineering reference for maintaining and extending the dashboard. For install/usage docs see the [root README](../README.md).

## Process model

Two executables, one deployable unit:

- **`EliteDashboard.Host`** — a single ASP.NET Core (.NET 9) process: REST API, SignalR hub, several `BackgroundService`s (journal ingestion, EDDN listener, Inara sync, reference-data import), and — once published — the built React SPA served as static files from the same Kestrel port. This is the only piece that does real work, and it's fully headless-testable (`dotnet run`, no tray required).
- **`EliteDashboard.Tray`** (net9.0-windows, WinForms) — a thin launcher/status icon around the Host, run as a separate OS process (see `HostProcessManager`). It starts `EliteDashboard.Host.exe` as a child process, captures its stdout/stderr to `%LOCALAPPDATA%\EliteDashboard\logs\host.log`, and offers a tray menu (open dashboard, show LAN QR code, restart, view logs). It never touches journal data or the database itself — it only talks to the Host over HTTP/process control. Kept separate so the Host stays plain and swappable.

There is deliberately no separate "API server" vs "worker" split — for a single-user personal app, one process with several background services is simpler to run and deploy than a distributed system, and there's no scaling requirement that would justify the extra complexity.

## Project layout

```
src/
  EliteDashboard.Domain/    In-memory state model (StateStore, DashboardState, ship/rank/economic
                            snapshots). No EF Core or web dependencies - pure data + reconciliation
                            target, easy to reason about in isolation.
  EliteDashboard.Journal/   Journal tailing, event parsing, snapshot-file (Status.json) watching,
                            historical backfill, and JournalStateReconciler (turns parsed events
                            into StateStore updates and DB writes).
  EliteDashboard.Data/      EF Core DbContext, entities, migrations, the single-writer DB queue.
  EliteDashboard.Trading/   EDDN listener, trade route engine, trade plan/progress tracking. Depends
                            on EliteDashboard.External for shared system-coordinate resolution
                            (SystemCoordinateResolver) - the only reason this project reaches outside
                            Domain/Data.
  EliteDashboard.External/  Everything that talks to a third-party HTTP API: FDevIDs/coriolis-data
                            reference import, Inara sync, EDSM coordinate lookups
                            (SystemCoordinateResolver, shared with the trade route engine), and the
                            colonisation shipping planner.
  EliteDashboard.Host/      Composition root - Program.cs wires everything up, minimal-API
                            endpoints (Endpoints/), SignalR hub (Hubs/).
  EliteDashboard.Tray/      WinForms tray launcher (separate process, production-only concern).
web/
  src/
    pages/                  One component per nav tab (DashboardPage, EconomicsPage, FleetPage, ...)
    use*.ts                 One data hook per feature, each owning its own fetch + SignalR wiring
    api.ts                  Shared fetch helpers, API base URL, small formatting utilities
    signalr.ts              Single shared HubConnection
deploy/                     publish.ps1 / install.ps1 / uninstall.ps1 - see deploy/README.md
fixtures/                   Real/derived journal files used as replay input for manual verification
```

## Data flow: journal → browser

1. **`JournalTailer`** watches the Saved Games journal directory. It replays the *current* file from byte offset 0 on startup (journal files are self-contained per session/part — `Fileheader`/`LoadGame`/etc. are re-asserted at the top of every file) and then tails new writes. It follows session/part rotation by re-resolving "the latest `Journal.*.log` file by `LastWriteTimeUtc`" on every tick — not by watching for a specific next filename. A `FileSystemWatcher` reduces latency, but a 1-second poll timer (`JournalOptions.PollIntervalMs`) is the actual source of truth, since `FileSystemWatcher` is known to silently drop events under load; every code path funnels through the same `Tick()` method regardless of which one triggered it.
2. Each complete line becomes a `JournalLine` event, parsed by **`JournalEventParser`** into a typed record (`FSDJumpEvent`, `DockedEvent`, etc.) via a big `switch` on the journal's `"event"` field. Unrecognized event types become `UnknownEvent` rather than throwing — the journal schema is large and still evolving (Colonisation and Powerplay 2.0 were both added mid-project), so a parser that can't fail on unknown input is a hard requirement, not a nicety.
3. **`JournalStateReconciler`** is the one place "what does this event mean for current state" logic lives. For every event (known or not) it writes the raw JSON to `JournalEventRaw` (the audit log / schema-drift safety net — if a future event type matters and isn't modeled yet, the raw row is still there to backfill from) and, for known event types, updates `StateStore` (in-memory, for instant UI push) and/or enqueues a DB write for anything that needs to persist (transactions, ship specs, colonisation sites, etc.).
4. **`StateStore`** raises a `Changed` event on every update; `Program.cs` wires that straight to `hubContext.Clients.All.SendAsync("DashboardStateChanged", state)`. Same pattern for `TransactionFeed.Added` and `TradeRoutePlanService.ProgressChanged`.
5. The React SPA's `useDashboardState()` (and sibling `use*` hooks) hold a `HubConnection` from `signalr.ts`, apply pushed messages directly to local state, and fall back to an initial REST fetch on mount. There's no client-side polling anywhere — every view update is server-pushed.

Snapshot files (`Status.json`) are handled separately by `SnapshotWatcher<T>`, since they're overwritten-in-place rather than appended — a `FileSystemWatcher` + poll fallback re-reads and parses the whole file on change, same latency-vs-reliability tradeoff as the journal tailer.

### Historical backfill

`JournalTailer` only ever reads the *current* journal file — by design, since it needs to replay that file from scratch on every restart to rebuild `StateStore`. Left at that, years of real history sitting in other Journal files would never reach the Economics/Powerplay charts. `JournalBackfillService` (`EliteDashboard.Journal`) closes that gap:

- Runs on every startup, from `JournalIngestionService.ExecuteAsync`, *before* `JournalTailer.Start()`. It enumerates every `Journal.*.log` file except the current/latest one (chronological order isn't perfectly guaranteed across Frontier's old 2-digit-year filename format and the newer ISO-ish one, but this doesn't matter — see "Replay safety" below for why), keeps only those written after the high-water mark, and feeds each line through the same `JournalStateReconciler.Handle()` used for live tailing.
- Progress is tracked as a high-water mark — `AppSetting["Journal.BackfillProcessedThroughUtc"]`, the newest processed file-write time — so ~90k lines of history aren't rescanned on every startup. After the first full run, a startup typically processes zero files, or the one or two sessions played while the app was offline (their rotated files postdate the mark; the dedup guards make replaying their already-tailed lines cheap no-ops). Installs that ran the original once-ever backfill (`Journal.BackfillCompleteUtc`) get the mark seeded from that flag's timestamp on the first run of this version, which processes exactly the sessions the old backfill missed.
- Sets `StateStore.SuppressNotifications = true` for the duration, so an already-open dashboard doesn't flicker through years of the past — `JournalTailer`'s own replay of the *current* file runs immediately afterward (with notifications re-enabled) and leaves `Current` fully correct either way.
- **If you add a reconciler case that needs to see historical data** (as opposed to only caring about live/current state), it won't retroactively apply to already-backfilled installs until the mark is reset once: `DELETE FROM AppSettings WHERE Key = 'Journal.BackfillProcessedThroughUtc'` (the next startup then replays all history again — idempotent writes make that safe). This is exactly what happened when `ColonisationContribution` tracking and `FSDJump` coordinate capture were added after the first backfill had already run.

### Replay safety: idempotent writes are mandatory

Because both the live tailer and the backfill service can (and regularly do) reprocess the same journal line more than once across the app's lifetime, **every write a reconciler case makes must be safe to repeat**:

- Most state is naturally idempotent — upserts by a stable key (`Station` by `MarketId`, `ColonisationSite` by `MarketId`, `PowerplayStanding` singleton, etc.) just overwrite with the same values on replay. Nothing extra needed.
- Append-only logs are not naturally idempotent, and blindly `Add()`-ing on every replay was a real, shipped bug: transactions were tripling on every restart before this was fixed. `Transaction`, `PowerplayStandingHistory`, and `JournalEventRaw` all carry a `(JournalFile, LineNumber)` pair with a unique index; before inserting, the reconciler checks whether a row already exists for that exact source line and skips the insert if so (see `PersistAndPublishAsync`, `UpsertPowerplayStandingAsync`, and `Archive`). `ColonisationSite.HasContributed` (set by `MarkColonisationSiteContributedAsync`) is a plain idempotent boolean flip and doesn't need this treatment.
- **Rule of thumb:** if a new event handler does `db.Whatever.Add(new ...)` unconditionally instead of upserting by a stable key, thread the `JournalLine` through and guard it with a `(JournalFile, LineNumber)` existence check first. Skipping this doesn't just risk duplicates on a crash-restart loop — the historical backfill *guarantees* every historical line gets replayed at least once more the next time the high-water mark is reset for any reason.

### A third replay-safety rule: in-memory-only state needs its own persistence

The two rules above cover *what gets written to the DB*. There's a third failure mode specific to `StateStore` fields that have **no DB table backing them at all** - populated purely by a reconciler case mutating `StateStore` in memory, with nothing durable to fall back on. `DashboardState.Fleet` was exactly this for most of this project's life, and it was a real, shipped bug (fixed by the `Ship` entity/table): `StoredShips` is a *sparse*, player-triggered event (only fires when you open a shipyard's Ships panel), not part of the journal's per-session startup block the way `Rank`/`Statistics`/etc. are re-asserted at the top of every file. Once the file containing your last `StoredShips` visit aged past `JournalBackfillService`'s high-water mark - which happens automatically over time as you play - a plain process restart would replay only the *current* file (no `StoredShips` in it) and silently collapse `Fleet` down to just the currently-flown ship. `StateStore` itself is never persisted; it's rebuilt from `DashboardState.Empty` on every process start, so there was nothing to fall back on.

**The fix, and the pattern to follow for any future in-memory-only state:** persist a durable snapshot on every write (`JournalStateReconciler.PersistFleetSyncAsync`, called after both `StoredShips` and `Loadout` reconciliation - a full delete-and-reinsert sync, since `StoredShips` is authoritative for the *complete* fleet each time, including ships sold since the last sync), and seed `StateStore` from that table **before any journal replay happens** (`JournalStateReconciler.SeedFleetFromDatabaseAsync`, called from `JournalIngestionService.ExecuteAsync` ahead of both the backfill and the live tailer). Regression coverage: `tests/EliteDashboard.Tests/FleetPersistenceTests.cs`.

If you add a new reconciler case that updates `StateStore` from a **sparse** event (not part of the startup block, not re-asserted every session) and nothing else in the app persists that data independently, ask whether it needs the same treatment - otherwise it's one high-water-mark advance away from silently vanishing on restart.

Not everything needs the StateStore-seeding half of this pattern, though. `ShipLoadout` (the last full `Loadout` snapshot per ship: modules as JSON, plus cargo capacity / jump range / fuel tank / hull & module values / rebuy, and when it was seen) is persisted on every `Loadout` event (`JournalStateReconciler.PersistShipLoadoutAsync`, upsert keyed by `ShipId`, newest event timestamp wins) but is **never** held in `StateStore` - the only consumer is `GET /api/fleet/{shipId}/loadout`, which reads the table directly on demand. Skipping the in-memory copy keeps the pushed state lean; rows for ships that leave the fleet are pruned by the same `PersistFleetSyncAsync` resync that maintains `Ship`. Regression coverage: `tests/EliteDashboard.Tests/ShipLoadoutPersistenceTests.cs`.

### Known journal quirks this codebase already accounts for

- The refuel event is **`RefuelAll`** (with a `Cost` field), not `Refuel`.
- `ColonisationConstructionDepot` / `ColonisationContribution` carry only a `MarketID` — no system or station name — so colonisation sites are resolved/joined against the `Station` table (itself populated from your own `Docked` events, which do carry names).
- `EngineerCraft` doesn't always carry a cost field; it's persisted to `JournalEventRaw` but not forced into the `Transaction` ledger unless a cost is actually present.
- Frontier's own journal is inconsistent about ship-symbol casing between event types: `LoadGame`/`Loadout` write your *current* ship's type lowercase (e.g. `explorer_nx`), while `StoredShips` and FDevIDs-derived data use the mixed-case form (`Explorer_NX`). Anything joining on ship symbol must compare case-insensitively — see `FleetEndpoints.cs` and `web/src/useShipSpecs.ts` (`lookupShipSpec`) for the two places this is handled today.
- `Loadout` never carries a ship *display* name at all (unlike `LoadGame`'s `ShipLocalised`) — only the internal symbol. Swapping ships mid-session with no new `LoadGame` event means the top-level `ShipTypeDisplay` has no source to update from. `JournalStateReconciler.UpdateShipTypeDisplayAsync` resolves it the same way the Fleet view does — a lookup against `ShipStaticSpec` (the FDevIDs-derived table) — rather than leaving the *previous* ship's name stuck on screen.
- `StoredShips` only lists ships you're **not** currently flying, so the "currently flying" fleet entry is synthesized/maintained by the reconciler itself (`ReconcileFleetForCurrentShip`), not by `StoredShips`. Without this, swapping ships mid-session (no intervening shipyard visit to refresh `StoredShips`) leaves the *previous* ship marked as active in the Fleet view indefinitely.
- `Loadout` only ever describes the ship you're **sitting in** — there is no journal source of module detail for stored ships. That's why per-ship loadouts are snapshots keyed by `ShipId` with a `SeenAtUtc` timestamp (see the `ShipLoadout` note above): a stored ship's loadout is only as fresh as the last time you boarded it, and the Fleet page says so ("Last seen …", or "No loadout recorded" for a ship never boarded since tracking began).
- Two different "no custom name" sentinels exist for ship names depending on the event: an empty string `""` and a single space `" "`. A plain falsy/empty check misses the latter (a lone space is truthy in JS) and silently renders as invisible whitespace. `JournalStateReconciler.NormalizeName` treats both as `null`.
- Some structures embed an unlocalized panel-name token directly inside `StationName` itself, with no separate `_Localised` field to strip it from: a deployed colonisation ship's `StationName` is literally `"$EXT_PANEL_ColonisationShip; <your ship's name>"`. `JournalStateReconciler.CleanStationName` strips a leading `"$...; "` token when present.
- `ColonisationConstructionDepot` fires for **any** site with the contribution service that you've simply docked at — including ones you have zero involvement in. Naively tracking every site you've ever docked at pollutes "My Systems" with irrelevant construction sites. See the "Colonisation" section below for how this is actually gated.
- The `Rank` event (tier index, 0-8 for Combat/Trade/Explore/CQC, 0-14 for Federation/Empire) and the `Progress` event (percent toward the *next* tier) are separate, independently-timed events describing the same rank categories — both must merge onto the existing `RankStanding` (`s.Rank with { ... }`), not replace it wholesale, or whichever event arrives second wipes out the other's data. Tier display names (`web/src/rankLabels.ts`) are sourced from Frontier's own journal manual appendix, not a fan wiki — at least one community source disagrees with the official docs (Exploration tier 4 is officially "Explorer", not "Trailblazer").
- `RepairAll` (repair every module in one action) is a distinct event from `Repair` (a single module) - both carry a `Cost` field and both map to the same `TransactionCategory.Repair`.
- `BuyDrones`' cost field is named `TotalCost`, not `Cost` like every other purchase event (`BuyAmmo`, `Repair`, `RefuelAll`, ...) - easy to get wrong by pattern-matching the others.
- `Resurrect` (choosing a respawn option after `Died`) carries the real rebuy expense - `Died` itself has no cost field. `Option` is `"rebuy"` (real `Cost`) or `"free"` (no real cost); only record a transaction when `Cost > 0`. This was a real, large gap: a live rebuy (5.9M credits) was completely absent from Economics before `ResurrectEvent` was added.
- `Location` fires instead of `FSDJump`/`Docked` whenever a session starts already in a system - at login, or right after a `Resurrect` respawn. It's the only other source of `StarPos`/current-station info besides those two events; skipping it means `CurrentSystem`/`CurrentStation` show stale data from the *previous* session until the next real jump or dock.

## Database

SQLite via EF Core, WAL mode (`PRAGMA journal_mode=WAL`, enabled once at startup in `MigrateDashboardDataAsync`). Default location `%LOCALAPPDATA%\EliteDashboard\dashboard.db`, overridable via the `ConnectionStrings:Dashboard` configuration key.

### Single-writer queue

SQLite allows only one writer at a time, and `DbContext` isn't thread-safe, but several independent `BackgroundService`s (journal ingestion, EDDN listener, reference-data importer, Inara sync) all need to write. Rather than let them race:

- **`DbWriteQueue`** (`EliteDashboard.Data`) is an unbounded `Channel` of `(work, completion)` pairs. Anything that needs to write calls `EnqueueAsync(Func<DashboardDbContext, CancellationToken, Task>)`.
- **`DbWriterService`** is the sole `BackgroundService` that drains the channel, one item at a time, using `IDbContextFactory<DashboardDbContext>` to create a fresh context per unit of work.

Reads never go through the queue — anything read-only calls `IDbContextFactory.CreateDbContextAsync()` directly and concurrently. Only writes are serialized.

**Rule of thumb when adding new write logic:** never inject `DashboardDbContext` directly into a service that writes; inject `DbWriteQueue` and enqueue the write. Read-only endpoints/services can inject `IDbContextFactory<DashboardDbContext>` directly.

### Migrations

```bash
dotnet ef migrations add <Name> --project src/EliteDashboard.Data --startup-project src/EliteDashboard.Data
```

Migrations apply automatically at startup (`app.Services.MigrateDashboardDataAsync()` in `Program.cs`) — there's no separate manual migration step in deployment. `DesignTimeDbContextFactory` supplies a design-time context for the `dotnet ef` CLI (which doesn't run through `Program.cs`).

### DateTime convention

Every `DateTime` in the model is UTC by convention. SQLite has no timezone-aware column type, so values round-trip with `Kind=Unspecified` by default — which `System.Text.Json` then serializes *without* a `Z` suffix, and browsers misinterpret as local time. `DashboardDbContext.ConfigureConventions` applies a model-wide `UtcDateTimeConverter` so this is handled once rather than field-by-field. If you add a new `DateTime` property, you get this for free; you don't need to think about it.

## External data sources

| Source | Purpose | Refresh policy |
|---|---|---|
| **EDDN** (`EddnListener`, ZeroMQ via NetMQ) | Live, galaxy-wide commodity prices feeding the trade route engine. Also carries `stationType` (Frontier's own vocabulary, same as `Docked` events) - captured into `Station.StationType` whenever it's not already known from a real `Docked` event, which never gets downgraded by it | Standing subscription from startup — always fresh, no refresh concept applies. Runs its blocking receive loop on a dedicated `Thread` (not a pooled task) so a sustained blocking receive can't starve the thread pool. |
| **EDCD/FDevIDs** `shipyard.csv`/`outfitting.csv` + **EDCD/coriolis-data** ship JSON files (`ReferenceDataImporter`) | Ship stats (manufacturer, size class, hull mass/cost, base shield/armour, crew) for ships not necessarily owned, and module display names (`hpt_multicannon_gimbal_medium` → "Multi-Cannon") for the Fleet loadout view | Checked once at startup; re-imports if the last successful import is >7 days old **or** `ModuleStaticSpecs` is empty (`AppSetting["ShipSpecs.LastImportUtc"]`) — the latter check exists so adding the module import after ship specs were already fresh didn't silently skip forever. Fetches are bounded to 5 concurrent requests. The GitHub API call requires an explicit `User-Agent` header (default `HttpClient` sends none, which GitHub's API 403s) — see the named `HttpClient` registration in `EliteDashboard.External/ServiceCollectionExtensions.cs`. |
| **Inara** (`InaraClient`, `InaraSyncService`) | Pushes commander credits, ranks, Powerplay standing, and lifetime statistics to your own Inara profile | Every 10 minutes, no-ops entirely if no API key is configured (`AppSetting["Inara.ApiKey"]`, set via the Settings page). `isBeingDeveloped: true` is sent permanently — this is a one-off personal tool, never distributed or listed on Inara. |
| **EDSM** (`EdsmClient`, wrapped by `SystemCoordinateResolver`) | System coordinate lookups for both the colonisation shipping planner and the trade route engine, for systems only known via EDDN's price feed (never personally visited, so no `FSDJump` `StarPos` for them) | On-demand only, when a candidate supplier's/station's system position is needed. Result is cached permanently on the `StarSystem` row (`HasCoordinates`) once resolved, so the same system is never looked up twice. Not the same thing as EDSM's journal-sync feature (see Known gaps) - this only ever reads public system coordinates. |
| **elite.tromador.com** `System.csv`/`Station.csv` (`GalaxyDataImporter`) | Whole-galaxy bulk backfill of system coordinates and per-station max pad size/planetary flag - the same EDDN-sourced data as the other rows here, just pre-aggregated across every commander rather than resolved one system at a time | Checked once at startup; re-imports if >7 days old (`AppSetting["GalaxyData.LastImportUtc"]`). Streams both CSVs (~14MB + ~110MB) rather than buffering them, and only ever enriches rows this app already has - see Reference data import below for why it can't create new `Station` rows. |

### Reference data import

`ReferenceDataImporter` deliberately **joins two independent, real data sources rather than hardcoding a symbol→stats mapping**: FDevIDs' `shipyard.csv` maps Frontier's internal ship symbol to the game's real display name, and coriolis-data's ship files are keyed by that display name. The join key is a normalized (alphanumeric-only, lowercased) form of the display name. Ships not yet catalogued in coriolis-data (typically the newest releases) simply get no row — that's correct behavior, not a bug; fabricating stats would be worse than omitting them. If ships are missing after a fresh import, check the log line `Matched {N}/{Total} ships against coriolis-data` to see how many resolved.

Module names are simpler: `outfitting.csv` maps a module's internal symbol directly to its display name, mount, class, and rating in one file — no second source to join against, since the journal's `Loadout` event gives modules the same "internal symbol only" treatment it gives ships. A handful of non-equipment loadout entries (`VesselVoice`, `ShipCockpit`, `CargoHatch`) aren't real outfitting items and have no display name to look up; they're shown as their raw value rather than fabricated, same philosophy as unmatched ships.

`GalaxyDataImporter` (also `EliteDashboard.External`) is strictly **enrichment**, unlike the ship-specs import above which fully replaces its tables on every run. Two things force that:

- **`StarSystem` coordinates** are only filled in when `HasCoordinates` is still `false` - the commander's own `FSDJump` history (and any prior `EdsmClient` resolution) is first-hand and always wins over this second-hand bulk data.
- **`Station.csv`'s own `station_id`** is EDDB's internal numbering scheme, with no relationship to Frontier's `MarketId` (this app's only canonical station key - see Colonisation). Station rows can't be joined by ID, and for the same reason this importer can never *create* a `Station` row - it has no `MarketId` to create one with. Instead rows are matched by `(system name, station name)` against stations this app already has, and only ever write to two dedicated columns, `Station.CommunityMaxPadSizeClass`/`CommunityPlanetary` - never the real `SmallPads`/`MediumPads`/`LargePads`/`StationType` fields sourced from actual `Docked` events, which stay strictly more authoritative (see Pad-size inference below).

Systems are always imported before stations in the same pass, since resolving a station's system name needs a `system_id → name` lookup that only exists once `System.csv` has been read. Both files use a MySQL-dump style of CSV, not RFC4180: string fields are wrapped in *single* quotes, with an embedded apostrophe doubled (`'Moxon''s Mojo'` → `Moxon's Mojo`) rather than backslash-escaped - a standard CSV parser would misparse this, hence the hand-rolled line parser in `GalaxyDataImporter.ParseLine`.

## Trade route engine

`TradeRouteEngine` (`EliteDashboard.Trading`) is a bounded-hop heuristic, not a provably-optimal solver: `FindSingleHopRoutesAsync` ranks every profitable `(buy, sell)` pair per commodity from the EDDN-fed `MarketPriceQuote` table that survives the criteria below; `FindMultiHopRouteAsync` greedily chains the best next single hop from the previous destination, up to a hop cap - it picks the single best *starting* hop within the origin radius (when no explicit origin station is given), then re-searches with `OriginStationId` pinned to the previous hop's destination for every subsequent leg. **Both `/api/trade/search` and `/api/trade/activate` always call `FindMultiHopRouteAsync`**, never the flat single-hop ranking directly - a route is one continuous circuit through the galaxy, not a list of unrelated one-off deals, so returning independent top-N single hops (an earlier bug: the endpoints used to branch on `OriginStationId is null` and call `FindSingleHopRoutesAsync` with `maxResults: MaxHops` in that case, silently producing a "top 3 best deals near me" list instead of a chained route) would defeat the entire point of a *multi*-hop planner. `TradeRoutePlanService` tracks progress by matching live `MarketBuy`/`MarketSell` events (via `JournalStateReconciler`) against the plan's next incomplete hop — there's exactly one `Status = Active` plan at a time.

Quantity per hop is `Min(CargoCapacity, Credits / BuyPrice, Supply, Demand)` - deliberately not filtering out zero entries before taking that minimum. `Supply`/`Demand` also gate which quotes are even considered as buy/sell candidates in the first place (`Supply > 0` for buy, `Demand > 0` for sell): EDDN (and the game's own market screen) can carry a nonzero cached price for a commodity that's currently out of stock or not wanted at all - confirmed against real data where a station's last-reported quote for a commodity had real buy/sell prices but `Supply = 0`, meaning it wasn't actually purchasable despite showing up as "available" by price alone. Trusting price fields without checking supply/demand was a real bug here, not just a hypothetical.

### Search criteria

`TradeRouteSearchCriteria` (a plain record, not persisted) carries everything the frontend's settings panel exposes. `GET /api/trade/defaults` seeds sensible values from the live `StateStore` (current system, cargo capacity, credits, jump range) and a case-insensitive `ShipStaticSpec` lookup by the current ship's internal symbol (for pad size); the user can override any of them before searching.

- **Origin radius** ("start within N jumps of current location", default 5) — when no explicit origin station is given, candidate buy stations are limited to `MaxJumpsFromOrigin × ShipJumpRangeLy` straight-line light-years of `OriginSystemName`. An explicit origin (via `OriginStationId`/`OriginMarketId`, e.g. when chaining multi-hop legs) bypasses the radius entirely.
- **Hop distance cap** ("jumps between stations", default 2) — every buy→sell pair, at every hop, is capped at `MaxJumpsBetweenStations × ShipJumpRangeLy`.
- **Pad size** (`RequiredShipSizeClass`, matches `ShipStaticSpec.SizeClass`: 1/2/3) — filters both ends of every hop via `StationCapabilities.SupportsShipSize` (see below). `IncludeUnknownPadSize` (default `false`) controls whether genuinely-unresolvable stations are excluded (the safe default — a route through a station your ship can't actually dock at is worse than a shorter list) or included anyway.
- **No planets / no fleet carriers** (`ExcludePlanetary`/`ExcludeFleetCarriers`, both default `true`) — via `StationCapabilities.IsPlanetary`/`IsFleetCarrier`, keyed off `Station.StationType`.

Both distance-based filters are a deliberate approximation: straight-line ("as the crow flies") distance between system coordinates, not an actual plotted jump route, and `ShipJumpRangeLy` is whatever the journal's `Loadout` event last reported (not necessarily fully-laden with the specific cargo a given hop would carry). Good enough to bound the search space sensibly; not a substitute for actually plotting the route in-game before committing to it.

`GET /api/trade/coverage` answers "how much of the galaxy can we currently do this for" - it counts, among every system with at least one priced station, how many already have `HasCoordinates = true` versus not. The frontend surfaces this directly in the search settings hint rather than leaving "only cover systems we already know the location of" as an undiscoverable implementation detail. Re-fetched after every search, since a search can itself resolve new systems.

### Pad-size inference (`StationCapabilities`, `EliteDashboard.Data`)

Most candidate stations for a trade search only ever come from EDDN, not personal `Docked` events, so exact per-station pad counts (`Station.SmallPads`/`MediumPads`/`LargePads`, populated only from your own `Docked` events' `LandingPads` block) are usually unavailable. `StationCapabilities` falls back through two more tiers, most authoritative first:

1. **Real per-station pad counts** (`Station.LargePads` etc.) — from your own `Docked` events. Always wins when present.
2. **Community-sourced data** (`Station.CommunityMaxPadSizeClass`/`CommunityPlanetary`, from `GalaxyDataImporter` — see Reference data import) — crowd-sourced across every commander who's ever docked there, not just this one, so it covers most stations this app knows about at all, not just the reliable-type subset below.
3. **`Station.StationType`** — only for the subset of types where that's actually reliable, verified against the commander's own real per-station pad-count history: `Coriolis`/`Orbis`/`Ocellus`/`Dodec`/`AsteroidBase`/`FleetCarrier` reliably support Large (confirmed: every real dock at one of these had ≥1 Large pad); `Outpost` reliably does **not** (confirmed: every real Outpost dock had exactly 0). Every other type — notably `CraterOutpost` and `SurfaceStation` — is treated as **unknown** at this tier, because real data showed both varying wildly (`CraterOutpost` from 0 to 4 Large pads, `SurfaceStation` from 0 to 16, for the *same* type). Guessing for these would be actively wrong some of the time, not just imprecise.

Small and Medium ships are always treated as supported absent real evidence otherwise — every observed station type had at least one pad of each. Genuinely unknown (no real data, no community data, no reliable type) only remains for stations `GalaxyDataImporter` hasn't matched yet, or has no community data for either.

This is why EDDN's `stationType` field (see External data sources) matters here even without any community/EDSM data: most trade candidates never come from a personal dock, so without it, `StationType` would be `null` for nearly everything and Large-ship pad filtering would have almost nothing to work with.

## Colonisation

### Site visibility: HasContributed OR claimed system

`ColonisationConstructionDepot` fires for *any* site with the contribution service you've docked at, regardless of involvement — tracking every site that ever produces this event would fill "My Systems" with construction sites you merely passed through once. A site is only surfaced (`GET /api/systems/sites`) if:

1. **`ColonisationSite.HasContributed` is true** — set only by a real `ColonisationContribution` event (actual cargo delivered), never by `ConstructionDepot` alone. This is the "I'm actively hauling for this" signal.
2. **or** its system matches an unreleased row in `ColonisationSystemClaims` — set by `ColonisationSystemClaimEvent`, which fires only when *you* personally claim a system for colonisation. This is the "I'm the architect" signal, and covers a brand-new build from the moment you start it, before your first delivery lands (which `HasContributed` alone can't do).

`ColonisationSystemClaim.Released` exists in the schema but nothing currently sets it `true` — Frontier's journal doesn't appear to have a "claim released/abandoned" event this app handles yet, so in practice condition 2 is "every system I've ever claimed." Harmless in the common case (claims are rarely abandoned), but worth knowing if a system that's genuinely no longer yours starts showing up.

Both signals are OR'd together in `ColonisationEndpoints.MapGet("/sites")` itself (not in the reconciler) — the underlying `ColonisationSite` row is created and kept up to date regardless of either flag, same as before; only the *listing* endpoint filters.

### Shipping planner

`ColonisationShippingPlanner` (`EliteDashboard.External` — grouped there since it leans on `EdsmClient`, not because it's ship/module reference data) answers "where do I go and what do I buy" for a site's outstanding materials, via `POST /api/systems/sites/{id}/shipping-plan`. Computed fresh on every call, not persisted (unlike `TradeRoutePlan` — a colonisation shipping list is a one-way shopping trip ending in a delivery, not a buy/sell profit loop, so the existing trade-plan persistence/progress-tracking model doesn't fit).

Per outstanding resource:

1. Normalize the resource's raw token name (`"$aluminium_name;"` → `"aluminium"`) and resolve it to `Commodity` row IDs, matched case-insensitively — EDDN senders are inconsistent about commodity name casing, which has left duplicate `Commodity` rows for the same real commodity (e.g. both `aluminium` and `Aluminium`) as a known, unfixed data-quality wrinkle. The planner works around it by matching case-insensitively rather than fixing it at ingestion.
2. Query `MarketPriceQuote` for sellers with stock, from the last 3 days only (`MaxQuoteAge`) — this is meant to reflect **current** availability, not a stale galaxy-wide directory.
3. Split candidates into "system coordinates already known" (free — from the commander's own `FSDJump` history) and "unknown" (would need an EDSM lookup). **Only bother with EDSM if there aren't already enough known candidates** (`MinKnownCandidatesBeforeSkippingEdsm = 20`), and even then only for a bounded, concurrency-limited batch (`MaxEdsmLookupsPerCommodity = 12`, 5 at a time). This bound is load-bearing, not cosmetic: a common commodity like Aluminium had 500+ galaxy-wide EDDN listings in testing, and the first unbounded/sequential version of this code took minutes to respond because of it.
4. Distance is plain 3D Euclidean over `StarSystem.X/Y/Z`. Coordinates come from two places: the commander's own `FSDJump` events (`StarPos`, free, populated in `JournalStateReconciler`'s `FsdJumpEvent` case) for systems actually visited, or an on-demand `EdsmClient` lookup (cached permanently once resolved) for systems only known via EDDN. `StarSystem.HasCoordinates` exists because `(0,0,0)` is Sol's *real* position — indistinguishable from "never set" without an explicit flag.
5. Prefer non-planetary suppliers: try orbital-only candidates first, and only fall back to including planetary ones (`StationCapabilities.IsPlanetary`: `SurfaceStation`, `CraterPort`, `CraterOutpost`, `OnFootSettlement`) if no orbital option exists for that commodity at all. `Station.StationType` is populated both from your own `Docked` events and, now, from EDDN's `stationType` field (see External data sources) whenever it wasn't already known — so most EDDN-only candidates have a real type rather than `null`. A candidate with a still-unknown type is treated as viable/orbital by default rather than excluded.
6. The best (nearest, orbital-preferred) station per commodity is picked, then results are **grouped by station** (not by commodity) into stops, ordered nearest-first from the site's own system — a greedy per-commodity heuristic, not a true multi-stop route optimizer, same "bounded heuristic over optimal solver" tradeoff as `TradeRouteEngine`.

## Ship art

`ShipImageEndpoints` (`GET /api/ships/{shipType}/image`) serves a badge image per ship. No stable, legal source of real ship artwork was found to fully automate this: EDAssets.org is a JS-rendered SPA with no discoverable API, and the Fandom wiki's article pages sit behind Cloudflare bot protection - though its static asset CDN (`static.wikia.nocookie.net`) does not: direct image URLs fetch fine with a normal user agent (verified 2026-08-04, when the current by-type art was pulled from exactly there), so hand-picked wiki images are a viable manual source. Rather than hardcode brittle hotlink URLs of uncertain licensing, real art is instead **user-supplied and never checked into the repo** - it's personal to each install, under `%LOCALAPPDATA%\EliteDashboard\ship-art\`:

- `by-type\{Ship type display name}.{webp|png|jpg|jpeg}` - generic art for any ship of that type.
- `by-ship\{Ship's given name}.{webp|png|jpg|jpeg}` - a specific owned hull (your paint job, your own screenshot), keyed by the name you gave it in-game.

Lookup order per request: `by-ship` first (keyed by the ship's name, from the frontend's `shipName` query param - only applies if you've named that ship), then `by-type` (keyed by the ship's display name, falling back to its internal Frontier symbol if that's what you'd rather name the file). Both directories are re-scanned on every request rather than cached in memory - there are only ever a handful of files for a personal fleet, and it means a newly dropped-in file shows up immediately with no app restart. Anything matching neither falls through to the original generated-placeholder behavior (a colored SVG shape, keyed by `ShipStaticSpec.SizeClass` - circle/rounded-square/hexagon for small/medium/large - cached to disk under `ship-images\`).

Matching is normalized (letters/digits only, lowercased) on both the lookup key and each candidate filename's stem, so `Krait Mk II.webp`, `krait-mk-ii.png`, and `KRAITMKII.jpg` are all treated as the same key - deliberately forgiving, since these filenames are meant to be typed by hand, not generated.

Because the live folder sits in app data, a wholesale wipe of `%LOCALAPPDATA%\EliteDashboard` (e.g. an over-broad database reset) destroys the art with everything else - and unlike the DB, it cannot be rebuilt from journal replay. Mitigation: a git-ignored backup copy under the repo's `ship-art\` (same `by-type`/`by-ship` layout), which `deploy\publish.ps1` restores additively into the live folder at the end of every publish. Art dropped straight into the live folder still works instantly; copy it into the repo folder too if it should survive a wipe.

## Frontend conventions

- One hook per feature (`useDashboardState`, `useEconomics`, `useTrade`, `useColonisation`, `usePowerplay`, `useShipSpecs`, `useWishlist`, `useInaraSettings`), each owning its own initial REST fetch and any SignalR event subscriptions relevant to it. There's no global client-side store — `signalr.ts` exports one shared `HubConnection`, and each hook attaches/detaches its own listeners.
- Plain `fetch`, no data-fetching library — the app's real-time behavior comes from SignalR pushes, not query invalidation/refetching, so React Query's main value proposition doesn't apply here.
- The dev frontend (`npm run dev`, Vite on port 5173) and the production build (served from the Host's `wwwroot`) are the same code; `api.ts`'s `apiBaseUrl` is what differs between them.
- PWA service worker (`vite-plugin-pwa`) uses `NetworkOnly` for any `/api/` or `/hubs/` path — this is a live dashboard, so nothing live is ever allowed to serve a stale cached response. Only the static app shell is cached for installability.
- React Router unmounts a page component when you navigate away from its route, so plain `useState` for anything a user would expect to "stick" (a filter, a selected range) silently resets on the next visit. `chartRange.ts`'s `usePersistedChartRange` is the established pattern for this: state backed by `localStorage` under a page-specific key, surviving both route changes and full page reloads. Reach for it any time new UI state should feel like a remembered preference rather than a one-off toggle.

## Adding a new journal event end-to-end

1. Add a record to `EliteDashboard.Journal/Events/JournalEvent.cs` matching the event's real JSON shape (check a real captured line in your own journal first — field presence and casing vary more than the official docs suggest).
2. Register it in the `switch` in `JournalEventParser.Parse`.
3. Add a `case` in `JournalStateReconciler.Handle` for whatever state/DB changes it should cause. If it needs to persist anything, enqueue the write via `DbWriteQueue` — never hold a `DashboardDbContext` directly in the reconciler.
4. **If the write is an upsert by a stable key (most cases), nothing else to do.** If it's an append-only insert instead (a new history/log table, not "update the current value of X"), it is *not* safe by default — the tailer replays the current file from scratch on every restart, and the historical backfill can replay everything else at least once more later. Thread the `JournalLine` through and guard the insert with a `(JournalFile, LineNumber)` existence check first (see `PersistAndPublishAsync` or `Archive`). Skipping this is exactly how transactions ended up tripling on every restart before it was fixed.
5. If it should show up in the Economics ledger, call `RecordTransaction(...)` with a `TransactionCategory` (add a new one if none fits).
6. If new state needs to reach the frontend, add it to the relevant `Domain` snapshot record and make sure `StateStore.Update` includes it — it'll ride the existing `DashboardStateChanged` push automatically.
7. Add a REST endpoint under `Endpoints/` only if the frontend needs to query it independently of the pushed state (e.g. paginated history).
8. Verify against real data: drop a captured (or hand-edited, schema-faithful) line into a scratch journal file under `fixtures/` and point `Journal:JournalPathOverride` at it (see below). If the event needs to apply to *historical* data too (not just newly-tailed lines), remember the backfill won't reprocess already-backfilled installs until `AppSettings["Journal.BackfillProcessedThroughUtc"]` is deleted once (see "Historical backfill" above).

## Verification approach

A small automated suite exists — `tests/EliteDashboard.Tests` (xUnit; real EF model against shared-cache in-memory SQLite via the `TestDb` helper, the real `DbWriterService` draining the write queue) plus vitest for the frontend (`web/`, `npm test`) — focused on regression-prone behavior (tailer crash-safety, reconciler null-data tolerance, backfill high-water mark, quote staleness cutoff, ship loadout persistence/pruning, SignalR reconnect resync, persisted trade-search criteria). Coverage is deliberately thin: this is a personal single-user app, and the highest-value verification is still against real journal data, which the project has in abundance:

- **`fixtures/`** holds real (or minimally hand-edited, schema-faithful) journal files used as replay input. Point `Journal:JournalPathOverride` (config or `Journal__JournalPathOverride` env var) at a fixture directory to make the Host tail it instead of your live journal.
- For a genuinely live check, run the Host against your real Saved Games folder and confirm the relevant view updates within about a second of an in-game action — this project's own Phase 1–8 development was verified this way throughout (fleet roster against real owned ships, Powerplay standing against real merit totals, colonisation sites against real construction sites, etc.).
- The EDDN listener logs `EDDN listener connected to tcp://eddn.edcd.io:9500` and exposes `LastMessageAtUtc`/`MessagesReceived`/`CommodityMessagesProcessed` — useful to confirm the feed is actually flowing before trusting trade route output.
- When adding automated tests, `EliteDashboard.Domain` (no EF/web dependencies), `JournalEventParser`/`JournalStateReconciler` (given a `JournalOptions.JournalPathOverride` pointed at a fixture), and the `TestDb` in-memory-SQLite harness are the most test-friendly seams.
- **Simulating states your own journal doesn't currently have** (e.g. a colonisation site with outstanding materials, when your real one just happens to be complete): back up `dashboard.db` first, then hand-edit the relevant row(s) directly via a throwaway `Microsoft.Data.Sqlite` console app (the NuGet packages are already restored locally via `EliteDashboard.Data`'s own dependency on them, so this needs no network access) — verify the real behavior, then restore from the backup or write the values back. This is how the shipping planner and the colonisation site-visibility rules were both verified end-to-end against real data without waiting for an actual in-game construction project to reach the right state.

## Deployment internals

`deploy/publish.ps1` does, in order: `npm run build` (→ `web/dist`), copies `web/dist` into `src/EliteDashboard.Host/wwwroot` (Program.cs's `UseStaticFiles`/`MapFallbackToFile` then serve it — this only matters for a published build, since `dotnet build`/`dotnet run` leave `wwwroot` empty and those middleware calls are no-ops), then `dotnet publish` for both `EliteDashboard.Host` and `EliteDashboard.Tray` into the same output directory (`HostProcessManager` looks for `EliteDashboard.Host.exe` next to the Tray executable in production; set `ELITEDASHBOARD_HOST_PATH` to override for dev-mode tray testing).

`deploy/install.ps1` (Administrator) registers a logon-triggered Scheduled Task rather than a Windows Service specifically because the journal lives under the user profile — a service running as `LocalSystem` would need extra configuration to even see `Saved Games`, while a logon-triggered task just runs in the user's own session.

## Known gaps / deferred work

- **Frontier Companion API (CAPI)** — would enable live market/shipyard/fleet-carrier data beyond what the journal exposes, but needs OAuth2+PKCE app registration. Not started.
- **EDSM journal sync** — not started; a different feature from the system-coordinate lookups `EdsmClient` already does (see External data sources) — this would be EDSM's full journal-upload integration, mirroring the Inara sync pattern. Independent of Inara sync either way.
- **Automated test coverage is thin** — the suite (see Verification approach) targets a handful of regression-prone paths rather than the whole app; fixture/live verification remains the primary check.
- **Trade route engine is heuristic, not optimal** — fine for personal use; a proper multi-hop solver (or wrapping `trade-dangerous` as an alternate engine) would be the next step if route quality becomes a real constraint.
- **Trade route/shipping planner distances are straight-line, not plotted jump routes** — a reasonable proxy for bounding search radius and hop distance, but not a substitute for actually plotting a route in-game. Both also only consider systems whose coordinates are already known — from a personal `FSDJump`, a bounded EDSM lookup (see Search criteria under Trade route engine), or `GalaxyDataImporter`'s weekly bulk backfill (see External data sources) — a station in a system none of those three has ever priced coordinates for simply won't appear as a candidate.
- **Pad size for stations you've never docked at falls back to community data, then station type, and only reliably for some types** — `GalaxyDataImporter` covers most known stations (see Pad-size inference under Trade route engine), but for anything it hasn't matched, `CraterOutpost` and `SurfaceStation` in particular were confirmed (via real per-station data) to vary too much by individual station to infer from type alone, so they're treated as unknown pad size rather than guessed.
- **`ColonisationSystemClaim.Released` is never set** — no currently-handled journal event represents abandoning/releasing a colonisation claim, so a system stays "yours" (for site-visibility purposes) forever once claimed. Low-impact today, but worth revisiting if a released claim ever causes a stale site to linger.
