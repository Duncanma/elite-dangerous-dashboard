using System.Text.Json.Serialization;
using EliteDashboard.Data;
using EliteDashboard.Domain;
using EliteDashboard.External;
using EliteDashboard.Host.Endpoints;
using EliteDashboard.Host.Hubs;
using EliteDashboard.Journal;
using EliteDashboard.Trading;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDashboardData(builder.Configuration);
builder.Services.AddTrading();
builder.Services.AddJournalIngestion(builder.Configuration);
builder.Services.AddExternalIntegrations();

// Enums as strings on the wire (both the REST endpoint and SignalR payloads) -
// far less fragile for the frontend than relying on numeric enum ordinals.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// LAN-only personal app, no auth: allow any origin so the SPA (served from a
// different dev-server port, or from any device's browser) can reach the API/hub.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

await app.Services.MigrateDashboardDataAsync();

app.UseCors();

// Serves the built SPA (web/dist copied to wwwroot at publish time) so the
// published app is a single deployable on one port. In dev, the frontend
// runs separately via the Vite dev server instead - wwwroot is empty then,
// so these are no-ops rather than errors.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<DashboardHub>("/hubs/dashboard");

app.MapGet("/api/state", (StateStore store) => Results.Ok(store.Current));
app.MapEconomicsEndpoints();
app.MapFleetEndpoints();
app.MapShipImageEndpoints();
app.MapTradeEndpoints();
app.MapColonisationEndpoints();
app.MapPowerplayEndpoints();
app.MapSettingsEndpoints();

// React Router client-side routing - any unmatched GET (e.g. a browser
// refresh on /trade) falls back to index.html rather than a 404.
app.MapFallbackToFile("index.html");

var stateStore = app.Services.GetRequiredService<StateStore>();
var transactionFeed = app.Services.GetRequiredService<TransactionFeed>();
var tradeRoutePlanService = app.Services.GetRequiredService<TradeRoutePlanService>();
var hubContext = app.Services.GetRequiredService<IHubContext<DashboardHub>>();
stateStore.Changed += state => _ = hubContext.Clients.All.SendAsync("DashboardStateChanged", state);
transactionFeed.Added += record => _ = hubContext.Clients.All.SendAsync("TransactionAdded", record);
tradeRoutePlanService.ProgressChanged += progress => _ = hubContext.Clients.All.SendAsync("TradeRouteProgressChanged", progress);

app.Run();
