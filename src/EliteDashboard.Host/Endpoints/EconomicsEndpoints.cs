using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Host.Endpoints;

public sealed record CreditGoalRequest(long TargetAmount, DateTimeOffset? TargetDate, string? Note);

public sealed record BalancePoint(DateTimeOffset TimestampUtc, long Balance);

public static class EconomicsEndpoints
{
    public static void MapEconomicsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/economics");

        group.MapGet("/transactions", async (
            IDbContextFactory<DashboardDbContext> factory,
            TransactionCategory? category,
            int limit) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var query = db.Transactions.AsNoTracking().OrderByDescending(t => t.TimestampUtc).AsQueryable();
            if (category is { } c)
            {
                query = query.Where(t => t.Category == c);
            }

            var rows = await query.Take(limit <= 0 ? 200 : Math.Min(limit, 1000)).ToListAsync();
            return Results.Ok(rows);
        });

        group.MapGet("/summary", async (IDbContextFactory<DashboardDbContext> factory, StateStore store) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var transactions = await db.Transactions.AsNoTracking().OrderBy(t => t.TimestampUtc).ToListAsync();

            // Only a subset of credit-affecting events are captured as
            // Transactions so far (see JournalStateReconciler) - this working
            // backwards from the current live balance keeps the series
            // approximately accurate rather than starting from an arbitrary
            // zero, while staying simple to extend as more categories are added.
            var currentBalance = store.Current.Economy.Credits;
            var recordedDelta = transactions.Sum(t => t.Amount);
            var runningBalance = currentBalance - recordedDelta;

            var points = new List<BalancePoint>(transactions.Count + 1);
            if (transactions.Count > 0)
            {
                points.Add(new BalancePoint(AsUtcOffset(transactions[0].TimestampUtc), runningBalance));
            }

            foreach (var t in transactions)
            {
                runningBalance += t.Amount;
                points.Add(new BalancePoint(AsUtcOffset(t.TimestampUtc), runningBalance));
            }

            return Results.Ok(points);
        });

        group.MapGet("/goals", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var goals = await db.CreditGoals.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.TargetAmount).ToListAsync();
            return Results.Ok(goals);
        });

        group.MapPost("/goals", async (IDbContextFactory<DashboardDbContext> factory, CreditGoalRequest request) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var goal = new CreditGoal
            {
                TargetAmount = request.TargetAmount,
                TargetDate = request.TargetDate?.UtcDateTime,
                Note = request.Note,
                CreatedUtc = DateTime.UtcNow,
            };
            db.CreditGoals.Add(goal);
            await db.SaveChangesAsync();
            return Results.Created($"/api/economics/goals/{goal.Id}", goal);
        });

        group.MapDelete("/goals/{id:long}", async (IDbContextFactory<DashboardDbContext> factory, long id) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var goal = await db.CreditGoals.FindAsync(id);
            if (goal is null)
            {
                return Results.NotFound();
            }

            goal.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static DateTimeOffset AsUtcOffset(DateTime utc) => new(utc, TimeSpan.Zero);
}
