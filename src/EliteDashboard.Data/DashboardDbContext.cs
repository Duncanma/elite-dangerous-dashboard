using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EliteDashboard.Data;

public sealed class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options)
    {
    }

    public DbSet<JournalEventRaw> JournalEvents => Set<JournalEventRaw>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CreditGoal> CreditGoals => Set<CreditGoal>();
    public DbSet<ShipWishlistItem> ShipWishlistItems => Set<ShipWishlistItem>();
    public DbSet<ShipUpgradePlan> ShipUpgradePlans => Set<ShipUpgradePlan>();
    public DbSet<StarSystem> StarSystems => Set<StarSystem>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Commodity> Commodities => Set<Commodity>();
    public DbSet<MarketPriceQuote> MarketPriceQuotes => Set<MarketPriceQuote>();
    public DbSet<TradeRoutePlan> TradeRoutePlans => Set<TradeRoutePlan>();
    public DbSet<TradeRouteHop> TradeRouteHops => Set<TradeRouteHop>();
    public DbSet<ColonisationSite> ColonisationSites => Set<ColonisationSite>();
    public DbSet<ColonisationResourceRequirement> ColonisationResourceRequirements => Set<ColonisationResourceRequirement>();
    public DbSet<ColonisationSystemClaim> ColonisationSystemClaims => Set<ColonisationSystemClaim>();
    public DbSet<PowerplayStanding> PowerplayStandings => Set<PowerplayStanding>();
    public DbSet<PowerplayStandingHistory> PowerplayStandingHistories => Set<PowerplayStandingHistory>();
    public DbSet<ShipStaticSpec> ShipStaticSpecs => Set<ShipStaticSpec>();
    public DbSet<ModuleStaticSpec> ModuleStaticSpecs => Set<ModuleStaticSpec>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Ship> Ships => Set<Ship>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no timezone-aware column type, so DateTime values round-trip
        // with Kind=Unspecified, which System.Text.Json then serializes without a
        // "Z" suffix - the browser would misread those as local time. Every
        // DateTime in this app is UTC by convention, so tag it as such on the way
        // out (and normalize on the way in) once, model-wide.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JournalEventRaw>(entity =>
        {
            entity.HasIndex(e => new { e.EventType, e.TimestampUtc });

            // Prevents the tailer's on-restart full replay of the current
            // journal file from re-archiving lines it already recorded.
            entity.HasIndex(e => new { e.JournalFile, e.LineNumber }).IsUnique();
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(e => e.Category).HasConversion<string>();
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => e.Category);

            // Same replay-dedup guard as JournalEventRaw. Nullable so rows
            // written before this key existed don't collide with each other
            // (SQLite treats NULLs as distinct in a unique index).
            entity.HasIndex(e => new { e.JournalFile, e.LineNumber }).IsUnique();
        });

        modelBuilder.Entity<StarSystem>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Station>(entity =>
        {
            entity.HasIndex(e => e.MarketId).IsUnique();
            entity.HasOne(e => e.StarSystem).WithMany(s => s.Stations).HasForeignKey(e => e.StarSystemId);
        });

        modelBuilder.Entity<Commodity>(entity =>
        {
            entity.HasIndex(e => e.InternalSymbol).IsUnique();
        });

        modelBuilder.Entity<Ship>(entity =>
        {
            entity.HasIndex(e => e.ShipId).IsUnique();
        });

        modelBuilder.Entity<MarketPriceQuote>(entity =>
        {
            entity.HasIndex(e => new { e.StationId, e.CommodityId }).IsUnique();
            entity.HasOne(e => e.Station).WithMany().HasForeignKey(e => e.StationId);
            entity.HasOne(e => e.Commodity).WithMany().HasForeignKey(e => e.CommodityId);
        });

        modelBuilder.Entity<TradeRoutePlan>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<TradeRouteHop>(entity =>
        {
            entity.HasOne(e => e.Plan).WithMany(p => p.Hops).HasForeignKey(e => e.TradeRoutePlanId);
        });

        modelBuilder.Entity<ColonisationSite>(entity =>
        {
            entity.HasIndex(e => e.MarketId).IsUnique();
        });

        modelBuilder.Entity<ColonisationResourceRequirement>(entity =>
        {
            entity.HasOne(e => e.Site).WithMany(s => s.Resources).HasForeignKey(e => e.ColonisationSiteId);
        });

        modelBuilder.Entity<ColonisationSystemClaim>(entity =>
        {
            entity.HasIndex(e => e.StarSystem);
        });

        modelBuilder.Entity<PowerplayStandingHistory>(entity =>
        {
            entity.HasIndex(e => e.TimestampUtc);
            entity.HasIndex(e => new { e.JournalFile, e.LineNumber }).IsUnique();
        });

        modelBuilder.Entity<ShipStaticSpec>(entity =>
        {
            entity.HasIndex(e => e.Symbol).IsUnique();
        });

        modelBuilder.Entity<ModuleStaticSpec>(entity =>
        {
            entity.HasIndex(e => e.Symbol).IsUnique();
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
        });
    }

    private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            toProvider => toProvider.Kind == DateTimeKind.Utc ? toProvider : toProvider.ToUniversalTime(),
            fromProvider => DateTime.SpecifyKind(fromProvider, DateTimeKind.Utc))
        {
        }
    }
}
