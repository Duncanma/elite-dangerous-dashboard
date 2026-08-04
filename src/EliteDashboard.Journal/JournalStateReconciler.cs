using System.Text.Json;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using EliteDashboard.Journal.Events;
using EliteDashboard.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.Journal;

/// <summary>
/// Applies parsed journal events to the shared StateStore, and archives every
/// line (known or not) to JournalEventRaw via the single-writer queue. Kept
/// separate from JournalTailer so the tailer stays a dumb line-source and all
/// "what does this event mean for current state" logic lives in one place.
/// </summary>
public sealed class JournalStateReconciler
{
    private readonly StateStore _store;
    private readonly DbWriteQueue _writeQueue;
    private readonly TransactionFeed _transactionFeed;
    private readonly TradeRoutePlanService _tradeRoutePlanService;
    private readonly IDbContextFactory<DashboardDbContext> _dbContextFactory;
    private readonly ILogger<JournalStateReconciler> _logger;
    private Dictionary<string, string>? _shipDisplayNamesBySymbol;

    public JournalStateReconciler(
        StateStore store, DbWriteQueue writeQueue, TransactionFeed transactionFeed,
        TradeRoutePlanService tradeRoutePlanService, IDbContextFactory<DashboardDbContext> dbContextFactory,
        ILogger<JournalStateReconciler> logger)
    {
        _store = store;
        _writeQueue = writeQueue;
        _transactionFeed = transactionFeed;
        _tradeRoutePlanService = tradeRoutePlanService;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public void HandleStatusSnapshot(Events.StatusFileSnapshot snapshot)
    {
        _store.Update(s => s with
        {
            ShipStatus = new ShipStatusSnapshot
            {
                Flags = snapshot.Flags,
                CargoTons = snapshot.Cargo,
                FuelMain = snapshot.Fuel?.FuelMain ?? s.ShipStatus.FuelMain,
                LegalState = snapshot.LegalState,
            },
        });
    }

    public void Handle(JournalLine line)
    {
        JournalEvent parsed;
        try
        {
            parsed = JournalEventParser.Parse(line.Json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse journal line {LineNumber} in {File}", line.LineNumber, line.FilePath);
            return;
        }

        Archive(line, parsed);
        Apply(line, parsed);
    }

    private void Archive(JournalLine line, JournalEvent parsed)
    {
        // Fire-and-forget from the tailer's perspective - the DbWriterService
        // serializes actual writes, this just enqueues. Not awaited here since
        // Handle() is called synchronously from the tailer's event.
        _ = _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            // The tailer replays the current journal file from the start on
            // every app restart (needed to rebuild in-memory state) - without
            // this check, every restart mid-session would re-archive every
            // line already recorded.
            var alreadyArchived = await db.JournalEvents.AnyAsync(
                e => e.JournalFile == line.FilePath && e.LineNumber == line.LineNumber, ct);
            if (alreadyArchived)
            {
                return;
            }

            db.JournalEvents.Add(new JournalEventRaw
            {
                TimestampUtc = parsed.Timestamp.UtcDateTime,
                EventType = parsed.Event,
                RawJson = line.Json,
                JournalFile = line.FilePath,
                LineNumber = line.LineNumber,
            });
            await db.SaveChangesAsync(ct);
        });
    }

    private void Apply(JournalLine line, JournalEvent parsed)
    {
        switch (parsed)
        {
            case CommanderEvent e:
                _store.Update(s => s with { FID = e.FID, Name = e.Name, LastUpdatedUtc = e.Timestamp });
                break;

            case LoadGameEvent e:
                _store.Update(s => s with
                {
                    FID = e.FID,
                    Name = e.Commander,
                    ShipType = e.Ship,
                    ShipTypeDisplay = e.ShipLocalised,
                    ShipName = NormalizeName(e.ShipName),
                    Economy = s.Economy with { Credits = e.Credits },
                    LastUpdatedUtc = e.Timestamp,
                });
                break;

            case FsdJumpEvent e:
                _store.Update(s => s with { CurrentSystem = e.StarSystem, Docked = false, CurrentStation = null, LastUpdatedUtc = e.Timestamp });
                if (e.StarSystem is not null && e.StarPos is { Length: 3 } pos)
                {
                    _ = UpsertSystemCoordinatesAsync(e.StarSystem, pos[0], pos[1], pos[2]);
                }

                break;

            case DockedEvent e:
                _store.Update(s => s with { CurrentSystem = e.StarSystem, CurrentStation = e.StationName, Docked = true, LastUpdatedUtc = e.Timestamp });
                if (e.MarketID != 0 && e.StationName is not null && e.StarSystem is not null)
                {
                    _ = UpsertStationFromDockAsync(e.MarketID, e.StationName, e.StationType, e.StarSystem, e.LandingPads);
                }

                break;

            case UndockedEvent e:
                _store.Update(s => s with { Docked = false, CurrentStation = null, LastUpdatedUtc = e.Timestamp });
                break;

            // Fires instead of FSDJump/Docked when a session starts already in
            // a system (login, or right after a Resurrect respawn) - the only
            // other source of current-location/coordinates.
            case LocationEvent e:
                _store.Update(s => s with
                {
                    CurrentSystem = e.StarSystem,
                    CurrentStation = e.Docked ? e.StationName : null,
                    Docked = e.Docked,
                    LastUpdatedUtc = e.Timestamp,
                });
                if (e.StarSystem is not null && e.StarPos is { Length: 3 } locationPos)
                {
                    _ = UpsertSystemCoordinatesAsync(e.StarSystem, locationPos[0], locationPos[1], locationPos[2]);
                }

                if (e.Docked && e.MarketID != 0 && e.StationName is not null && e.StarSystem is not null)
                {
                    _ = UpsertStationFromDockAsync(e.MarketID, e.StationName, e.StationType, e.StarSystem, null);
                }

                break;

            case RankEvent e:
                // Merge onto the existing Rank rather than replacing it - the
                // Progress event (in-tier percent) arrives separately and
                // shouldn't be wiped out by a later Rank update.
                _store.Update(s => s with
                {
                    Rank = s.Rank with
                    {
                        Combat = e.Combat,
                        Trade = e.Trade,
                        Explore = e.Explore,
                        CQC = e.CQC,
                        Federation = e.Federation,
                        Empire = e.Empire,
                    },
                    LastUpdatedUtc = e.Timestamp,
                });
                break;

            case ProgressEvent e:
                _store.Update(s => s with
                {
                    Rank = s.Rank with
                    {
                        CombatProgress = e.Combat,
                        TradeProgress = e.Trade,
                        ExploreProgress = e.Explore,
                        CqcProgress = e.CQC,
                        FederationProgress = e.Federation,
                        EmpireProgress = e.Empire,
                    },
                    LastUpdatedUtc = e.Timestamp,
                });
                break;

            case StatisticsEvent e when e.BankAccount is { } bank:
                _store.Update(s => s with
                {
                    Economy = s.Economy with
                    {
                        CurrentWealth = bank.CurrentWealth,
                        SpentOnShips = bank.SpentOnShips,
                        SpentOnOutfitting = bank.SpentOnOutfitting,
                        SpentOnRepairs = bank.SpentOnRepairs,
                        SpentOnFuel = bank.SpentOnFuel,
                        SpentOnAmmoConsumables = bank.SpentOnAmmoConsumables,
                        InsuranceClaims = bank.InsuranceClaims,
                    },
                    LastUpdatedUtc = e.Timestamp,
                });
                break;

            case LoadoutEvent e:
                _store.Update(s => s with
                {
                    ShipId = e.ShipID,
                    ShipType = e.Ship,
                    ShipName = NormalizeName(e.ShipName),
                    ShipIdent = e.ShipIdent,
                    CargoCapacity = e.CargoCapacity,
                    MaxJumpRange = e.MaxJumpRange,
                    Rebuy = e.Rebuy,
                    HullValue = e.HullValue,
                    ModulesValue = e.ModulesValue,
                    // STJ honors an explicit JSON null even though the record
                    // initializes Modules to [] - treat null as empty.
                    CurrentShipModules = (e.Modules ?? []).Select(ToModuleSummary).ToList(),
                    Fleet = ReconcileFleetForCurrentShip(s, e),
                    LastUpdatedUtc = e.Timestamp,
                });
                _ = PersistFleetSyncAsync(_store.Current.Fleet);
                _ = PersistShipLoadoutAsync(e);

                // Unlike LoadGame's ShipLocalised, Loadout never carries a
                // display name for the ship - without this, swapping ships
                // mid-session (no new LoadGame event) leaves the *previous*
                // ship's display name stuck on screen indefinitely.
                _ = UpdateShipTypeDisplayAsync(e.ShipID, e.Ship);
                break;

            case StoredShipsEvent e:
                _store.Update(s => s with
                {
                    Fleet = BuildFleet(s, e),
                    LastUpdatedUtc = e.Timestamp,
                });
                _ = PersistFleetSyncAsync(_store.Current.Fleet);
                break;

            case MarketBuyEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.TradeBuy, -e.TotalCost,
                    $"Bought {e.Count} x {e.TypeLocalised ?? e.Type}");
                if (e.Type is { } buyType)
                {
                    _ = _tradeRoutePlanService.HandleMarketBuyAsync(e.MarketID, buyType);
                }

                break;

            case MarketSellEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.TradeSell, e.TotalSale,
                    $"Sold {e.Count} x {e.TypeLocalised ?? e.Type}");
                if (e.Type is { } sellType)
                {
                    _ = _tradeRoutePlanService.HandleMarketSellAsync(e.MarketID, sellType);
                }

                break;

            case RedeemVoucherEvent e:
                RecordTransaction(line, e.Timestamp, CategorizeVoucher(e.Type), e.Amount,
                    $"Redeemed {e.Type} voucher");
                break;

            case MissionCompletedEvent e when e.Reward > 0:
                RecordTransaction(line, e.Timestamp, TransactionCategory.MissionReward, e.Reward,
                    e.LocalisedName is { } name ? $"Mission reward: {name}" : "Mission reward");
                break;

            case RefuelAllEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.Refuel, -e.Cost, "Refuelled");
                break;

            case RepairEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.Repair, -e.Cost, "Ship repair");
                break;

            case RepairAllEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.Repair, -e.Cost, "Repaired all modules");
                break;

            case BuyAmmoEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.Rearm, -e.Cost, "Restocked ammunition");
                break;

            case BuyDronesEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.Rearm, -e.TotalCost, "Bought limpet drones");
                break;

            // "free" respawns (e.g. covered by insurance in some circumstances)
            // carry no real Cost - only record an actual rebuy expense.
            case ResurrectEvent e when e.Cost > 0:
                RecordTransaction(line, e.Timestamp, TransactionCategory.Rebuy, -e.Cost, $"Ship rebuy ({e.Option})");
                break;

            case SearchAndRescueEvent e when e.Reward > 0:
                RecordTransaction(line, e.Timestamp, TransactionCategory.SearchAndRescue, e.Reward,
                    e.NameLocalised is { } srName ? $"Search and Rescue: {srName}" : "Search and Rescue");
                break;

            case NpcCrewPaidWageEvent e when e.Amount > 0:
                RecordTransaction(line, e.Timestamp, TransactionCategory.CrewWages, -e.Amount,
                    e.NpcCrewName is { } crewName ? $"Crew wage: {crewName}" : "Crew wage");
                break;

            case ShipyardBuyEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.ShipyardBuy, -e.ShipPrice,
                    $"Bought {e.ShipType}");
                break;

            case ShipyardSellEvent e:
                RecordTransaction(line, e.Timestamp, TransactionCategory.ShipyardSell, e.SellPrice,
                    $"Sold {e.ShipType}");
                break;

            case ColonisationConstructionDepotEvent e:
                _ = UpsertColonisationSiteAsync(e);
                break;

            case ColonisationContributionEvent e:
                _ = MarkColonisationSiteContributedAsync(e.MarketID);
                break;

            case ColonisationSystemClaimEvent e when e.StarSystem is not null:
                _ = UpsertSystemClaimAsync(e.StarSystem, e.SystemAddress, e.Timestamp);
                break;

            case PowerplayEvent e when e.Power is not null:
                _ = UpsertPowerplayStandingAsync(line, e.Power, e.Rank, e.Merits, e.TimePledged, e.Timestamp, meritsDelta: 0);
                break;

            case PowerplayRankEvent e when e.Power is not null:
                _ = UpdatePowerplayRankAsync(e.Power, e.Rank, e.Timestamp);
                break;

            case PowerplayMeritsEvent e when e.Power is not null:
                _ = UpsertPowerplayStandingAsync(line, e.Power, rank: null, e.TotalMerits, timePledgedSeconds: null, e.Timestamp, e.MeritsGained);
                break;

            case FileheaderEvent e:
                _logger.LogInformation("New journal file header: part {Part}, game version {GameVersion}", e.Part, e.GameVersion);
                break;

            case UnknownEvent:
                // Expected and fine - only a subset of event types are modeled so far.
                // The raw line is archived regardless (see Archive above).
                break;
        }
    }

    private void RecordTransaction(JournalLine line, DateTimeOffset timestampUtc, TransactionCategory category, long amount, string description)
    {
        // Keep the live balance accurate between LoadGame/Statistics events -
        // none of the individual transaction events carry a running balance.
        _store.Update(s => s with { Economy = s.Economy with { Credits = s.Economy.Credits + amount }, LastUpdatedUtc = timestampUtc });

        // Publish only after the write completes and the entity has a real,
        // DB-assigned Id - otherwise the live-pushed record and a later
        // refetch would disagree on the row's identity (React key collisions,
        // duplicate-looking rows after a refresh).
        _ = PersistAndPublishAsync(line, timestampUtc, category, amount, description);
    }

    private async Task PersistAndPublishAsync(JournalLine line, DateTimeOffset timestampUtc, TransactionCategory category, long amount, string description)
    {
        var entity = new Data.Entities.Transaction
        {
            TimestampUtc = timestampUtc.UtcDateTime,
            Category = category,
            Amount = amount,
            Description = description,
            JournalFile = line.FilePath,
            LineNumber = line.LineNumber,
        };

        var isDuplicate = false;

        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            // The tailer replays the current journal file from the start on
            // every app restart - without this check, every restart
            // mid-session would re-record every transaction already logged.
            var existing = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(
                t => t.JournalFile == line.FilePath && t.LineNumber == line.LineNumber, ct);
            if (existing is not null)
            {
                isDuplicate = true;
                return;
            }

            db.Transactions.Add(entity);
            await db.SaveChangesAsync(ct);
        });

        if (isDuplicate)
        {
            return;
        }

        _transactionFeed.Publish(new TransactionRecord
        {
            Id = entity.Id,
            TimestampUtc = timestampUtc,
            Category = category,
            Amount = amount,
            Description = description,
        });
    }

    private async Task UpsertStationFromDockAsync(long marketId, string stationName, string? stationType, string starSystemName, LandingPadCounts? landingPads)
    {
        stationName = CleanStationName(stationName);

        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var system = await db.StarSystems.FirstOrDefaultAsync(s => s.Name == starSystemName, ct);
            if (system is null)
            {
                system = new StarSystem { Name = starSystemName, LastUpdatedUtc = DateTime.UtcNow };
                db.StarSystems.Add(system);
                await db.SaveChangesAsync(ct);
            }

            var station = await db.Stations.FirstOrDefaultAsync(s => s.MarketId == marketId, ct);
            if (station is null)
            {
                db.Stations.Add(new Station
                {
                    MarketId = marketId,
                    Name = stationName,
                    StationType = stationType,
                    StarSystemId = system.Id,
                    SmallPads = landingPads?.Small,
                    MediumPads = landingPads?.Medium,
                    LargePads = landingPads?.Large,
                    LastUpdatedUtc = DateTime.UtcNow,
                    LastUpdateSource = "OwnJournal",
                });
            }
            else
            {
                station.Name = stationName;
                station.StationType = stationType ?? station.StationType;
                station.StarSystemId = system.Id;
                if (landingPads is not null)
                {
                    station.SmallPads = landingPads.Small;
                    station.MediumPads = landingPads.Medium;
                    station.LargePads = landingPads.Large;
                }

                station.LastUpdatedUtc = DateTime.UtcNow;
                station.LastUpdateSource = "OwnJournal";
            }

            await db.SaveChangesAsync(ct);
        });
    }

    private async Task UpsertSystemCoordinatesAsync(string systemName, double x, double y, double z)
    {
        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var system = await db.StarSystems.FirstOrDefaultAsync(s => s.Name == systemName, ct);
            if (system is null)
            {
                system = new StarSystem { Name = systemName, LastUpdatedUtc = DateTime.UtcNow };
                db.StarSystems.Add(system);
            }

            system.X = x;
            system.Y = y;
            system.Z = z;
            system.HasCoordinates = true;
            system.LastUpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        });
    }

    private async Task UpsertColonisationSiteAsync(ColonisationConstructionDepotEvent e)
    {
        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var site = await db.ColonisationSites.Include(s => s.Resources)
                .FirstOrDefaultAsync(s => s.MarketId == e.MarketID, ct);

            if (site is null)
            {
                site = new ColonisationSite
                {
                    MarketId = e.MarketID,
                    FirstSeenUtc = DateTime.UtcNow,
                };
                db.ColonisationSites.Add(site);
            }

            site.ConstructionProgress = e.ConstructionProgress;
            site.ConstructionComplete = e.ConstructionComplete;
            site.ConstructionFailed = e.ConstructionFailed;
            site.LastUpdatedUtc = DateTime.UtcNow;

            // The event always carries the full current snapshot, not a delta -
            // replace the resource list wholesale rather than trying to diff it.
            db.ColonisationResourceRequirements.RemoveRange(site.Resources);
            site.Resources = (e.ResourcesRequired ?? []).Select(r => new ColonisationResourceRequirement
            {
                ColonisationSiteId = site.Id,
                CommodityName = r.Name ?? "unknown",
                CommodityDisplayName = r.NameLocalised ?? r.Name,
                RequiredAmount = r.RequiredAmount,
                ProvidedAmount = r.ProvidedAmount,
                Payment = r.Payment,
            }).ToList();

            await db.SaveChangesAsync(ct);
        });
    }

    private async Task MarkColonisationSiteContributedAsync(long marketId)
    {
        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var site = await db.ColonisationSites.FirstOrDefaultAsync(s => s.MarketId == marketId, ct);
            if (site is null)
            {
                // ColonisationContribution can theoretically arrive before
                // any ConstructionDepot snapshot - create a bare row so the
                // contribution isn't lost; the next depot snapshot fills in
                // progress/resources as usual.
                site = new ColonisationSite { MarketId = marketId, FirstSeenUtc = DateTime.UtcNow };
                db.ColonisationSites.Add(site);
            }

            site.HasContributed = true;
            site.LastUpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        });
    }

    private async Task UpsertSystemClaimAsync(string starSystem, long systemAddress, DateTimeOffset timestamp)
    {
        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var claim = await db.ColonisationSystemClaims.FirstOrDefaultAsync(c => c.StarSystem == starSystem, ct);
            if (claim is null)
            {
                db.ColonisationSystemClaims.Add(new ColonisationSystemClaim
                {
                    StarSystem = starSystem,
                    SystemAddress = systemAddress,
                    ClaimedUtc = timestamp.UtcDateTime,
                });
            }
            else
            {
                claim.Released = false;
                claim.ClaimedUtc = timestamp.UtcDateTime;
            }

            await db.SaveChangesAsync(ct);
        });
    }

    private async Task UpsertPowerplayStandingAsync(JournalLine line, string power, int? rank, long totalMerits, long? timePledgedSeconds, DateTimeOffset timestamp, long meritsDelta)
    {
        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var standing = await db.PowerplayStandings.FirstOrDefaultAsync(ct);
            if (standing is null)
            {
                standing = new PowerplayStanding { Power = power };
                db.PowerplayStandings.Add(standing);
            }

            standing.Power = power;
            standing.TotalMerits = totalMerits;
            if (rank is not null)
            {
                standing.Rank = rank.Value;
            }

            if (timePledgedSeconds is not null)
            {
                standing.TimePledgedSeconds = timePledgedSeconds.Value;
            }

            standing.LastUpdatedUtc = DateTime.UtcNow;

            // The standing upsert above is idempotent (safe to re-apply on
            // replay); only the append-only history log needs a dedup guard.
            var alreadyLogged = await db.PowerplayStandingHistories.AnyAsync(
                h => h.JournalFile == line.FilePath && h.LineNumber == line.LineNumber, ct);
            if (!alreadyLogged)
            {
                db.PowerplayStandingHistories.Add(new PowerplayStandingHistory
                {
                    TimestampUtc = timestamp.UtcDateTime,
                    Power = power,
                    TotalMerits = totalMerits,
                    MeritsDelta = meritsDelta,
                    JournalFile = line.FilePath,
                    LineNumber = line.LineNumber,
                });
            }

            await db.SaveChangesAsync(ct);
        });
    }

    private async Task UpdatePowerplayRankAsync(string power, int rank, DateTimeOffset timestamp)
    {
        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var standing = await db.PowerplayStandings.FirstOrDefaultAsync(ct);
            if (standing is null)
            {
                standing = new PowerplayStanding { Power = power };
                db.PowerplayStandings.Add(standing);
            }

            standing.Power = power;
            standing.Rank = rank;
            standing.LastUpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        });
    }

    /// <summary>
    /// StoredShips only lists ships NOT currently being flown, so the
    /// currently-flown ship's Fleet entry has to be maintained here instead.
    /// Without this, swapping ships mid-session (no intervening StoredShips
    /// visit, e.g. no return trip to the shipyard tab) leaves the fleet
    /// list's "currently flying" flag stuck on the ship you swapped out of.
    /// </summary>
    private static IReadOnlyList<ShipSummary> ReconcileFleetForCurrentShip(DashboardState s, LoadoutEvent e)
    {
        var found = false;
        var updated = s.Fleet.Select(ship =>
        {
            if (ship.ShipId == e.ShipID)
            {
                found = true;
                return ship with { Location = ShipLocationKind.Current, StarSystem = s.CurrentSystem, Station = s.CurrentStation };
            }

            // Ship swaps happen while docked, so the ship just swapped out of
            // is most likely now sitting in storage at the current station -
            // the next real StoredShips visit corrects this if that's wrong.
            return ship.Location == ShipLocationKind.Current
                ? ship with { Location = ShipLocationKind.Here, StarSystem = s.CurrentSystem, Station = s.CurrentStation }
                : ship;
        }).ToList();

        if (!found)
        {
            // A ship never seen in a StoredShips list yet (e.g. bought and
            // immediately flown away) - ShipTypeDisplay is resolved shortly
            // after by UpdateShipTypeDisplayAsync rather than guessed here.
            updated.Add(new ShipSummary
            {
                ShipId = e.ShipID,
                ShipType = e.Ship,
                Name = NormalizeName(e.ShipName),
                StarSystem = s.CurrentSystem,
                Station = s.CurrentStation,
                Location = ShipLocationKind.Current,
            });
        }

        return updated;
    }

    private async Task UpdateShipTypeDisplayAsync(int shipId, string? shipSymbol)
    {
        if (shipSymbol is null)
        {
            return;
        }

        var displayName = await ResolveShipDisplayNameAsync(shipSymbol);

        // Guard against a race with a newer Loadout for a different ship
        // that may have already landed while this lookup was in flight.
        _store.Update(s => s.ShipType != shipSymbol ? s : s with
        {
            ShipTypeDisplay = displayName,
            Fleet = s.Fleet.Select(f => f.ShipId == shipId && f.ShipTypeDisplay is null
                ? f with { ShipTypeDisplay = displayName }
                : f).ToList(),
        });
    }

    private async Task<string?> ResolveShipDisplayNameAsync(string shipSymbol)
    {
        if (_shipDisplayNamesBySymbol is null)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var specs = await db.ShipStaticSpecs.AsNoTracking().ToListAsync();
            _shipDisplayNamesBySymbol = specs.ToDictionary(s => s.Symbol.ToLowerInvariant(), s => s.DisplayName);
        }

        return _shipDisplayNamesBySymbol.TryGetValue(shipSymbol.ToLowerInvariant(), out var name) ? name : null;
    }

    /// <summary>
    /// Most stations have a clean StationName with no localisation quirks,
    /// but some structures (e.g. deployed colonisation ships) embed an
    /// unlocalized panel-name token directly in StationName itself, with no
    /// separate _Localised field to fall back on:
    /// "$EXT_PANEL_ColonisationShip; Brule Reach" instead of just
    /// "Brule Reach". Strip a leading "$...; " token when present.
    /// </summary>
    private static string CleanStationName(string rawName)
    {
        if (rawName.Length == 0 || rawName[0] != '$')
        {
            return rawName;
        }

        var separatorIndex = rawName.IndexOf("; ", StringComparison.Ordinal);
        return separatorIndex >= 0 ? rawName[(separatorIndex + 2)..] : rawName;
    }

    private static TransactionCategory CategorizeVoucher(string? type) => type switch
    {
        "bounty" => TransactionCategory.BountyVoucher,
        "CombatBond" => TransactionCategory.BondVoucher,
        "scannable" => TransactionCategory.ExplorationVoucher,
        _ => TransactionCategory.Other,
    };

    private static IReadOnlyList<ShipSummary> BuildFleet(DashboardState current, StoredShipsEvent e)
    {
        var fleet = new List<ShipSummary>();

        // The ship currently being flown isn't listed in StoredShips at all -
        // add it explicitly so the fleet view is complete.
        if (current.ShipId is { } currentShipId)
        {
            fleet.Add(new ShipSummary
            {
                ShipId = currentShipId,
                ShipType = current.ShipType,
                ShipTypeDisplay = current.ShipTypeDisplay,
                Name = current.ShipName,
                StarSystem = current.CurrentSystem,
                Station = current.CurrentStation,
                Location = ShipLocationKind.Current,
            });
        }

        // ShipsHere/ShipsRemote can arrive as explicit JSON null (see Loadout).
        // Unlike ShipsRemote entries, the event names the exact station for
        // ShipsHere (it's wherever this StoredShips event fired), even though
        // that's a top-level field rather than per-ship.
        foreach (var ship in e.ShipsHere ?? [])
        {
            fleet.Add(ToSummary(ship, e.StarSystem, e.StationName, ShipLocationKind.Here));
        }

        foreach (var ship in e.ShipsRemote ?? [])
        {
            fleet.Add(ToSummary(ship, ship.StarSystem, null, ship.InTransit ? ShipLocationKind.InTransit : ShipLocationKind.Remote));
        }

        return fleet;
    }

    private static ShipSummary ToSummary(StoredShipEntry ship, string? starSystem, string? station, ShipLocationKind location) => new()
    {
        ShipId = ship.ShipID,
        ShipType = ship.ShipType,
        ShipTypeDisplay = ship.ShipTypeLocalised ?? ship.ShipType,
        Name = NormalizeName(ship.Name),
        Station = station,
        StarSystem = starSystem,
        Location = location,
    };

    private async Task PersistFleetSyncAsync(IReadOnlyList<ShipSummary> fleet)
    {
        var snapshot = fleet.Select(f => new Data.Entities.Ship
        {
            ShipId = f.ShipId,
            ShipType = f.ShipType,
            ShipTypeDisplay = f.ShipTypeDisplay,
            Name = f.Name,
            StarSystem = f.StarSystem,
            Station = f.Station,
            Location = f.Location.ToString(),
            LastUpdatedUtc = DateTime.UtcNow,
        }).ToList();

        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            // The reconciled in-memory fleet (from StoredShips or a Loadout
            // swap) is always the complete, authoritative membership at that
            // moment - a full resync, not a per-row upsert, is correct here,
            // including dropping ships that were sold since the last sync.
            db.Ships.RemoveRange(db.Ships);
            db.Ships.AddRange(snapshot);

            // Loadout snapshots follow fleet membership: a sold ship's
            // snapshot is useless and must not linger. Same idempotency as
            // the fleet resync itself - replaying the same events yields the
            // same end state either way.
            var fleetIds = fleet.Select(f => f.ShipId).ToList();
            await db.ShipLoadouts.Where(l => !fleetIds.Contains(l.ShipId)).ExecuteDeleteAsync(ct);

            await db.SaveChangesAsync(ct);
        });
    }

    /// <summary>
    /// Upserts the durable per-ship loadout snapshot from a Loadout event.
    /// Newest event timestamp wins: the high-water backfill plus live tail
    /// can surface older journal history after a newer snapshot is already
    /// stored, and replaying the same event must be a no-op - both are
    /// handled by the SeenAtUtc guard, keeping this safe under replay.
    /// </summary>
    private async Task PersistShipLoadoutAsync(LoadoutEvent e)
    {
        var modulesJson = JsonSerializer.Serialize((e.Modules ?? []).Select(ToModuleSummary).ToList());
        var seenAtUtc = e.Timestamp.UtcDateTime;

        await _writeQueue.EnqueueAsync(async (db, ct) =>
        {
            var existing = await db.ShipLoadouts.SingleOrDefaultAsync(l => l.ShipId == e.ShipID, ct);
            if (existing is not null && existing.SeenAtUtc > seenAtUtc)
            {
                return;
            }

            if (existing is null)
            {
                db.ShipLoadouts.Add(new ShipLoadout
                {
                    ShipId = e.ShipID,
                    ShipType = e.Ship,
                    SeenAtUtc = seenAtUtc,
                    CargoCapacity = e.CargoCapacity,
                    MaxJumpRange = e.MaxJumpRange,
                    FuelCapacityMain = e.FuelCapacity?.Main ?? 0,
                    UnladenMass = e.UnladenMass,
                    Rebuy = e.Rebuy,
                    HullValue = e.HullValue,
                    ModulesValue = e.ModulesValue,
                    ModulesJson = modulesJson,
                });
            }
            else
            {
                existing.ShipType = e.Ship;
                existing.SeenAtUtc = seenAtUtc;
                existing.CargoCapacity = e.CargoCapacity;
                existing.MaxJumpRange = e.MaxJumpRange;
                existing.FuelCapacityMain = e.FuelCapacity?.Main ?? 0;
                existing.UnladenMass = e.UnladenMass;
                existing.Rebuy = e.Rebuy;
                existing.HullValue = e.HullValue;
                existing.ModulesValue = e.ModulesValue;
                existing.ModulesJson = modulesJson;
            }

            await db.SaveChangesAsync(ct);
        });
    }

    /// <summary>
    /// Seeds StateStore.Fleet from the durable Ship table before any journal
    /// replay happens. See Ship's doc comment for why: StoredShips is a
    /// sparse, player-triggered event that can fall outside
    /// JournalBackfillService's high-water mark on a later restart, and
    /// StateStore never persists across restarts on its own - without this,
    /// the fleet would silently collapse to just the currently-flown ship.
    /// </summary>
    public async Task SeedFleetFromDatabaseAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var ships = await db.Ships.AsNoTracking().ToListAsync(ct);
        if (ships.Count == 0)
        {
            return;
        }

        var fleet = ships.Select(s => new ShipSummary
        {
            ShipId = s.ShipId,
            ShipType = s.ShipType,
            ShipTypeDisplay = s.ShipTypeDisplay,
            Name = s.Name,
            StarSystem = s.StarSystem,
            Station = s.Station,
            Location = Enum.TryParse<ShipLocationKind>(s.Location, out var kind) ? kind : ShipLocationKind.Remote,
        }).ToList();

        _store.Update(current => current with { Fleet = fleet });
    }

    /// <summary>
    /// The journal isn't consistent about how "no custom name" is represented
    /// - some events write "" and others write " " (a single space) - so a
    /// plain falsy/null check on the frontend isn't enough. Normalize both to
    /// null here, once, rather than at every display site.
    /// </summary>
    private static string? NormalizeName(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw;

    private static ShipModuleSummary ToModuleSummary(LoadoutModule module) => new()
    {
        Slot = module.Slot,
        Item = module.Item,
        On = module.On,
        Priority = module.Priority,
        Health = module.Health,
        EngineeringBlueprint = module.Engineering?.BlueprintName,
        EngineeringLevel = module.Engineering?.Level ?? 0,
        ExperimentalEffect = module.Engineering?.ExperimentalEffectLocalised,
    };
}
