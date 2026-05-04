using Loom.Agents.Anthropic;
using Loom.Agents.Foundry;
using Loom.Agents.InProc;
using Loom.Application.Abstractions;
using Loom.Domain.Common.DomainEvents;
using Loom.Infrastructure;
using Loom.Integrations;
using Loom.Mcp;
using Loom.Web.Auth;
using Loom.Web.Components;
using Loom.Web.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// ─── configuration ─────────────────────────────────────────────────────
// Connection string lookup order: env var (LOOM_CONNECTION) wins, then
// configuration ("ConnectionStrings:Loom"). The env-var path matters for
// containerised deployments and CI.
var connectionString =
    Environment.GetEnvironmentVariable("LOOM_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Loom")
    ?? throw new InvalidOperationException(
        "No database connection configured. Set LOOM_CONNECTION or " +
        "ConnectionStrings:Loom in configuration / user-secrets.");

// ─── auth ──────────────────────────────────────────────────────────────
// Entra ID (Azure AD) via Microsoft.Identity.Web. Configuration section
// "AzureAd" matches the convention; see appsettings.json for the shape.
// In Development, the AllowAnonymousFallback option lets developers run
// the app without a tenant by setting AzureAd:Enabled = false.
var azureAdEnabled = builder.Configuration.GetValue<bool>("AzureAd:Enabled", defaultValue: true);

if (azureAdEnabled)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}
else
{
    // Development-only escape hatch. Allows running the app without an
    // Entra tenant. Never enable in any environment that holds real data.
    builder.Services
        .AddAuthentication(DevAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
            DevAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();
}

builder.Services.AddCascadingAuthenticationState();

// ─── infrastructure (DbContext, repositories, clock) ───────────────────
builder.Services.AddLoomInfrastructure(connectionString);

// ─── agent runtimes ────────────────────────────────────────────────────
// Two streaming runtimes register side-by-side, plus the in-proc
// transcript normalizer for stage 0. The DefaultAgentRouter picks one
// per step based on WorkflowStep.EnginePref. Foundry (Azure OpenAI Chat
// Completions, ADR-0017) is the default for new workflow templates;
// Anthropic stays registered so workflows that pin it keep working.
builder.Services.AddAnthropicAgentRuntime(builder.Configuration);
builder.Services.AddFoundryAgentRuntime(builder.Configuration);
builder.Services.AddLoomInProcSteps();

// Phase-3 in-app notification channel: pushes per-user payloads via the
// NodeHub user-group connection. Teams + Email channels are stub
// implementations registered by AddLoomInfrastructure.
builder.Services.AddScoped<Loom.Application.Notifications.INotificationChannel, Loom.Web.Hubs.InAppNotificationChannel>();

// ─── domain event handlers + outbox dispatcher ────────────────────────
// Handlers must be registered BEFORE AddLoomOutboxDispatcher so the
// dispatcher's per-batch scope sees them.
builder.Services.AddScoped<IDomainEventHandler<NodeUpdated>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<NodeCreated>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<NodePhaseAdvanced>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<DiscoveryAccepted>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<RunStarted>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<RunCompleted>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<RunFailed>, NodeHubBroadcaster>();
builder.Services.AddScoped<IDomainEventHandler<RunPausedForHuman>, NodeHubBroadcaster>();
builder.Services.AddLoomOutboxDispatcher(builder.Configuration);

// ─── bootstrapper ─────────────────────────────────────────────────────
// Idempotent first-run setup: applies pending migrations, seeds the kickoff
// workflow + global bootstrap fragments. Default-on in Development; set
// Loom:Bootstrap:Enabled=false to disable in production deployments that
// run migrations through a separate channel.
builder.Services.AddLoomBootstrapper(builder.Configuration);

// ─── Phase-2 integrations + MCP ────────────────────────────────────────
// MCP server (in-process — see ADR-0008): exposes get_node_context,
// search_nodes, list_rules to IDE clients over /mcp. Integrations:
// LibGit2-backed GitSync writer + HttpClient-backed ADO adapter.
builder.Services.AddLoomMcpServer();
builder.Services.AddLoomIntegrations();

// ─── Blazor Server with interactive Server components ──────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();
builder.Services.AddControllers();

// ─── HTTP plumbing ─────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Loom.Web.Services.CurrentUserAccessor>();

var app = builder.Build();

// ─── pipeline ──────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// UseAuthentication runs in both modes — dev mode swaps in
// DevAuthenticationHandler, production uses Entra ID.
app.UseAuthentication();
app.UseAuthorization();

if (azureAdEnabled)
{
    app.MapControllers();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<NodeHub>("/hubs/node");

// MCP endpoints (HTTP/SSE transport, mounted at /mcp).
app.MapMcp("/mcp");

// Webhook intake controllers (anonymous — auth is HMAC-per-request).
app.MapControllers();

// Health endpoint — kept simple. OpenTelemetry / proper health checks
// arrive in a later slice.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>
/// Marker for WebApplicationFactory in integration tests.
/// </summary>
public partial class Program;
