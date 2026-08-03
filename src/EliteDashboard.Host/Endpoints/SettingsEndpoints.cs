using EliteDashboard.Data;
using EliteDashboard.Data.Entities;
using EliteDashboard.External;
using Microsoft.EntityFrameworkCore;

namespace EliteDashboard.Host.Endpoints;

public sealed record InaraSettingsRequest(string ApiKey);

public sealed record InaraSettingsResponse(bool HasApiKey, string? LastSyncStatus, DateTimeOffset? LastSyncAtUtc);

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings");

        group.MapGet("/inara", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var apiKey = await db.AppSettings.FindAsync(InaraSyncService.ApiKeySettingKey);
            var status = await db.AppSettings.FindAsync(InaraSyncService.LastSyncStatusSettingKey);
            var lastSync = await db.AppSettings.FindAsync(InaraSyncService.LastSyncAtSettingKey);

            DateTimeOffset? lastSyncAt = lastSync?.Value is { } v && DateTimeOffset.TryParse(v, out var parsed) ? parsed : null;

            return Results.Ok(new InaraSettingsResponse(
                HasApiKey: !string.IsNullOrWhiteSpace(apiKey?.Value),
                LastSyncStatus: status?.Value,
                LastSyncAtUtc: lastSyncAt));
        });

        group.MapPut("/inara", async (IDbContextFactory<DashboardDbContext> factory, InaraSettingsRequest request) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var setting = await db.AppSettings.FindAsync(InaraSyncService.ApiKeySettingKey);
            if (setting is null)
            {
                db.AppSettings.Add(new AppSetting { Key = InaraSyncService.ApiKeySettingKey, Value = request.ApiKey });
            }
            else
            {
                setting.Value = request.ApiKey;
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/inara", async (IDbContextFactory<DashboardDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var setting = await db.AppSettings.FindAsync(InaraSyncService.ApiKeySettingKey);
            if (setting is not null)
            {
                db.AppSettings.Remove(setting);
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        });
    }
}
