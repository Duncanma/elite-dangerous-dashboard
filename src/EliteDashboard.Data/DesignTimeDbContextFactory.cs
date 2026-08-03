using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EliteDashboard.Data;

/// <summary>
/// Lets `dotnet ef migrations add` construct a DbContext without needing the
/// full Host project's DI container - only used by EF Core design-time tooling.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DashboardDbContext>
{
    public DashboardDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new DashboardDbContext(options);
    }
}
