using System.Text.Json;
using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Host.Endpoints;

public sealed record WishlistItemRequest(string ShipType, string? DisplayName, string? TargetLoadoutNotes, long? EstimatedCost, int Priority);

public sealed record UpgradePlanRequest(
    int ShipId, string? ShipType, string ModuleSlot, string? CurrentModuleSymbol,
    string? DesiredModuleSymbol, string? DesiredEngineeringNotes, long? EstimatedCost, int Priority);

public static class FleetEndpoints
{
    public static void MapFleetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/fleet");

        group.MapGet("/", (StateStore store) => Results.Ok(new { fleet = store.Current.Fleet, currentShipModules = store.Current.CurrentShipModules }));

        // The last Loadout snapshot recorded for a ship, or 404 if none was
        // ever seen (stored ships only get one when boarded - the journal
        // exposes module detail for the flown ship only).
        group.MapGet("/{shipId:int}/loadout", async (IDbContextFactory<DashboardDbContext> factory, int shipId) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var loadout = await db.ShipLoadouts.AsNoTracking().SingleOrDefaultAsync(l => l.ShipId == shipId);
            if (loadout is null)
            {
                return Results.NotFound();
            }

            var modules = JsonSerializer.Deserialize<List<ShipModuleSummary>>(loadout.ModulesJson) ?? [];
            return Results.Ok(new
            {
                loadout.ShipId,
                loadout.ShipType,
                loadout.SeenAtUtc,
                loadout.CargoCapacity,
                loadout.MaxJumpRange,
                loadout.FuelCapacityMain,
                loadout.UnladenMass,
                loadout.Rebuy,
                loadout.HullValue,
                loadout.ModulesValue,
                Modules = modules,
            });
        });

        // Keyed by Frontier's internal ship symbol (ShipSummary.shipType) so
        // the frontend can enrich any ship card with a single extra fetch.
        // Ships not yet catalogued in coriolis-data are simply absent here.
        // Note: match case-insensitively - Frontier's own journal is
        // inconsistent about symbol casing between event types (e.g.
        // LoadGame/Loadout write "explorer_nx" lowercase for your current
        // ship, while StoredShips and these FDevIDs-derived keys use
        // "Explorer_NX"). See useShipSpecs.ts on the frontend.
        group.MapGet("/specs", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var specs = await db.ShipStaticSpecs.AsNoTracking().ToListAsync();
            return Results.Ok(specs.ToDictionary(s => s.Symbol, s => new
            {
                s.DisplayName,
                s.Manufacturer,
                s.SizeClass,
                s.HullMass,
                s.HullCost,
                s.BaseShieldStrength,
                s.BaseArmour,
                s.CrewSeats,
            }));
        });

        // Keyed by Frontier's internal module symbol (ShipModuleSummary.Item),
        // same case-insensitive-match caveat as /specs above - the Loadout
        // event always writes module symbols lowercase.
        group.MapGet("/module-specs", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var specs = await db.ModuleStaticSpecs.AsNoTracking().ToListAsync();
            return Results.Ok(specs.ToDictionary(s => s.Symbol, s => new
            {
                s.DisplayName,
                s.Category,
                s.Mount,
                s.Class,
                s.Rating,
            }));
        });

        group.MapGet("/wishlist", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var items = await db.ShipWishlistItems.AsNoTracking()
                .Where(i => i.PurchasedUtc == null)
                .OrderBy(i => i.Priority)
                .ToListAsync();
            return Results.Ok(items);
        });

        group.MapPost("/wishlist", async (IDbContextFactory<DashboardDbContext> factory, WishlistItemRequest request) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var item = new ShipWishlistItem
            {
                ShipType = request.ShipType,
                DisplayName = request.DisplayName,
                TargetLoadoutNotes = request.TargetLoadoutNotes,
                EstimatedCost = request.EstimatedCost,
                Priority = request.Priority,
                CreatedUtc = DateTime.UtcNow,
            };
            db.ShipWishlistItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/fleet/wishlist/{item.Id}", item);
        });

        group.MapPost("/wishlist/{id:long}/purchased", async (IDbContextFactory<DashboardDbContext> factory, long id) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var item = await db.ShipWishlistItems.FindAsync(id);
            if (item is null)
            {
                return Results.NotFound();
            }

            item.PurchasedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/wishlist/{id:long}", async (IDbContextFactory<DashboardDbContext> factory, long id) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var item = await db.ShipWishlistItems.FindAsync(id);
            if (item is null)
            {
                return Results.NotFound();
            }

            db.ShipWishlistItems.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapGet("/upgrade-plans", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var plans = await db.ShipUpgradePlans.AsNoTracking()
                .Where(p => p.CompletedUtc == null)
                .OrderBy(p => p.Priority)
                .ToListAsync();
            return Results.Ok(plans);
        });

        group.MapPost("/upgrade-plans", async (IDbContextFactory<DashboardDbContext> factory, UpgradePlanRequest request) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var plan = new ShipUpgradePlan
            {
                ShipId = request.ShipId,
                ShipType = request.ShipType,
                ModuleSlot = request.ModuleSlot,
                CurrentModuleSymbol = request.CurrentModuleSymbol,
                DesiredModuleSymbol = request.DesiredModuleSymbol,
                DesiredEngineeringNotes = request.DesiredEngineeringNotes,
                EstimatedCost = request.EstimatedCost,
                Priority = request.Priority,
                CreatedUtc = DateTime.UtcNow,
            };
            db.ShipUpgradePlans.Add(plan);
            await db.SaveChangesAsync();
            return Results.Created($"/api/fleet/upgrade-plans/{plan.Id}", plan);
        });

        group.MapPost("/upgrade-plans/{id:long}/complete", async (IDbContextFactory<DashboardDbContext> factory, long id) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var plan = await db.ShipUpgradePlans.FindAsync(id);
            if (plan is null)
            {
                return Results.NotFound();
            }

            plan.CompletedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/upgrade-plans/{id:long}", async (IDbContextFactory<DashboardDbContext> factory, long id) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var plan = await db.ShipUpgradePlans.FindAsync(id);
            if (plan is null)
            {
                return Results.NotFound();
            }

            db.ShipUpgradePlans.Remove(plan);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
