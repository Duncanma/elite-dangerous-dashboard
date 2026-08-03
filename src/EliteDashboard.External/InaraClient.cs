using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EliteDashboard.External;

public sealed record InaraEvent(string EventName, DateTimeOffset EventTimestamp, object EventData, int? EventCustomID = null);

public sealed record InaraEventResult(int EventStatus, string? EventStatusText, int? EventCustomID);

/// <summary>
/// Thin wrapper around Inara's documented JSON API (https://inara.cz/elite/inara-api-docs/).
/// One POST per sync batch - Inara explicitly asks integrations not to spam
/// individual events, so callers should batch (see InaraSyncService).
/// </summary>
public sealed class InaraClient
{
    private const string ApiUrl = "https://inara.cz/inapi/v1/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InaraClient> _logger;

    public InaraClient(IHttpClientFactory httpClientFactory, ILogger<InaraClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InaraEventResult>> SendEventsAsync(
        string apiKey, string? commanderName, IReadOnlyList<InaraEvent> events, CancellationToken ct = default)
    {
        var http = _httpClientFactory.CreateClient(nameof(InaraClient));

        var payload = new
        {
            header = new
            {
                appName = "Elite Dangerous Dashboard",
                appVersion = "1.0.0",
                // This is a one-off personal tool, never distributed or listed on
                // Inara - "true" is the semantically honest setting regardless of
                // how polished it is, and it tells Inara to skip global-event side
                // effects (community goals etc) that only make sense for real clients.
                isBeingDeveloped = true,
                APIkey = apiKey,
                commanderName,
            },
            events = events.Select(e => new
            {
                eventName = e.EventName,
                eventTimestamp = e.EventTimestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                eventCustomID = e.EventCustomID,
                eventData = e.EventData,
            }),
        };

        using var response = await http.PostAsJsonAsync(ApiUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        if (responseJson.TryGetProperty("header", out var header)
            && header.TryGetProperty("eventStatus", out var headerStatus)
            && headerStatus.GetInt32() != 200)
        {
            var text = header.TryGetProperty("eventStatusText", out var t) ? t.GetString() : null;
            _logger.LogWarning("Inara rejected the whole batch: {Status} {Text}", headerStatus.GetInt32(), text);
            return [];
        }

        var results = new List<InaraEventResult>();
        if (responseJson.TryGetProperty("events", out var eventsEl) && eventsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in eventsEl.EnumerateArray())
            {
                var status = e.TryGetProperty("eventStatus", out var s) ? s.GetInt32() : 0;
                var text = e.TryGetProperty("eventStatusText", out var t) ? t.GetString() : null;
                var customId = e.TryGetProperty("eventCustomID", out var c) ? c.GetInt32() : (int?)null;
                results.Add(new InaraEventResult(status, text, customId));

                if (status >= 400)
                {
                    _logger.LogWarning("Inara event failed: {Status} {Text}", status, text);
                }
            }
        }

        return results;
    }
}
