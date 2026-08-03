using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>Runs the ship-spec import once at startup, off the request path - the fleet view works fine without it, just with fewer stats until it completes.</summary>
public sealed class ReferenceDataStartupService : BackgroundService
{
    private readonly ReferenceDataImporter _importer;
    private readonly ILogger<ReferenceDataStartupService> _logger;

    public ReferenceDataStartupService(ReferenceDataImporter importer, ILogger<ReferenceDataStartupService> logger)
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
            _logger.LogWarning(ex, "Reference data startup import failed");
        }
    }
}
