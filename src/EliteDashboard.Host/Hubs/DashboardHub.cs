using Microsoft.AspNetCore.SignalR;

namespace EliteDashboard.Host.Hubs;

/// <summary>
/// Push channel for live dashboard state. Phase 1 only needs server-to-client
/// broadcasts (CommanderStateChanged); no client-invokable methods yet.
/// </summary>
public sealed class DashboardHub : Hub
{
}
