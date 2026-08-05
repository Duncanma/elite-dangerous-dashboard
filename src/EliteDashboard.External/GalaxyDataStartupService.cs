using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>Runs <see cref="GalaxyDataImporter"/> once at startup, in the background, without blocking the rest of the host.</summary>
public sealed class GalaxyDataStartupService : BackgroundService
{
    private readonly GalaxyDataImporter _importer;
    private readonly ILogger<GalaxyDataStartupService> _logger;

    public GalaxyDataStartupService(GalaxyDataImporter importer, ILogger<GalaxyDataStartupService> logger)
    {
        _importer = importer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _importer.ImportIfStaleAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Galaxy data startup import failed");
        }
    }
}
