using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.Data;

public sealed class DbWriterService : BackgroundService
{
    private readonly DbWriteQueue _queue;
    private readonly IDbContextFactory<DashboardDbContext> _contextFactory;
    private readonly ILogger<DbWriterService> _logger;

    public DbWriterService(DbWriteQueue queue, IDbContextFactory<DashboardDbContext> contextFactory, ILogger<DbWriterService> logger)
    {
        _queue = queue;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (work, completion) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var db = await _contextFactory.CreateDbContextAsync(stoppingToken);
                await work(db, stoppingToken);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB write command failed");
                completion.TrySetException(ex);
            }
        }
    }
}
