using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>
/// Resolves and persists a star system's galactic coordinates via EDSM, and
/// computes straight-line distance between two resolved systems. Shared by
/// every feature that needs real distances for systems we haven't personally
/// visited: the colonisation shipping planner and the trade route engine.
/// </summary>
public sealed class SystemCoordinateResolver
{
    private readonly DbWriteQueue _writeQueue;
    private readonly EdsmClient _edsm;
    private readonly ILogger<SystemCoordinateResolver> _logger;

    public SystemCoordinateResolver(DbWriteQueue writeQueue, EdsmClient edsm, ILogger<SystemCoordinateResolver> logger)
    {
        _writeQueue = writeQueue;
        _edsm = edsm;
        _logger = logger;
    }

    public async Task<bool> TryResolveAsync(StarSystem system, CancellationToken ct = default)
    {
        var resolved = await _edsm.GetSystemCoordinatesAsync(system.Name, ct);
        if (resolved is null)
        {
            _logger.LogDebug("EDSM has no coordinates for {System}", system.Name);
            return false;
        }

        var (x, y, z) = resolved.Value;
        system.X = x;
        system.Y = y;
        system.Z = z;
        system.HasCoordinates = true;

        var systemName = system.Name;
        await _writeQueue.EnqueueAsync(async (writeDb, innerCt) =>
        {
            var row = await writeDb.StarSystems.FirstOrDefaultAsync(s => s.Name == systemName, innerCt);
            if (row is null)
            {
                return;
            }

            row.X = x;
            row.Y = y;
            row.Z = z;
            row.HasCoordinates = true;
            await writeDb.SaveChangesAsync(innerCt);
        });

        return true;
    }

    public static double Distance(StarSystem a, StarSystem b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
