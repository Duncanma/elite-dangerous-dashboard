namespace EliteDashboard.Data.Entities;

/// <summary>Generic key/value store for small bits of runtime config (API keys, thresholds, etc).</summary>
public sealed class AppSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
}
