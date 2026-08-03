using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

/// <summary>
/// Looks up a star system's galactic coordinates from EDSM's public API.
/// Used only for systems we've never personally visited (and so have no
/// StarPos from our own FSDJump events for) but that show up as a candidate
/// commodity supplier via EDDN's price feed.
/// </summary>
public sealed class EdsmClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EdsmClient> _logger;

    public EdsmClient(IHttpClientFactory httpClientFactory, ILogger<EdsmClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(double X, double Y, double Z)?> GetSystemCoordinatesAsync(string systemName, CancellationToken ct = default)
    {
        try
        {
            var http = _httpClientFactory.CreateClient(nameof(EdsmClient));
            var url = $"https://www.edsm.net/api-v1/system?systemName={Uri.EscapeDataString(systemName)}&showCoordinates=1";
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            // EDSM returns "{}" for a system it doesn't know about.
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("coords", out var coords)
                || coords.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return (coords.GetProperty("x").GetDouble(), coords.GetProperty("y").GetDouble(), coords.GetProperty("z").GetDouble());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "EDSM system coordinate lookup failed for {System}", systemName);
            return null;
        }
    }
}
