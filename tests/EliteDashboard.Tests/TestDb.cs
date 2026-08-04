using EliteDashboard.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Tests;

/// <summary>
/// SQLite in-memory database (shared-cache, kept alive by an open keeper
/// connection) plus a matching IDbContextFactory, so tests exercise the real
/// EF model - unique indexes, UTC converters and all - without touching the
/// real dashboard.db. Each context gets its own connection so the writer
/// service and test assertions can run concurrently.
/// </summary>
internal sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _keeper;

    public TestDb()
    {
        var connectionString = $"Data Source=edd-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();

        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlite(connectionString)
            .Options;
        Factory = new TestDbContextFactory(options);

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public IDbContextFactory<DashboardDbContext> Factory { get; }

    public DashboardDbContext CreateContext() => Factory.CreateDbContext();

    public void Dispose() => _keeper.Dispose();

    private sealed class TestDbContextFactory(DbContextOptions<DashboardDbContext> options)
        : IDbContextFactory<DashboardDbContext>
    {
        public DashboardDbContext CreateDbContext() => new(options);

        public Task<DashboardDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
