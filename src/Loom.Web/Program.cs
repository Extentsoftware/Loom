using Loom.Infrastructure;
using Loom.Web.Components;
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
    builder.Services.AddAuthorization();
}

builder.Services.AddCascadingAuthenticationState();

// ─── infrastructure (DbContext, repositories, clock) ───────────────────
builder.Services.AddLoomInfrastructure(connectionString);

// ─── Blazor Server with interactive Server components ──────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── HTTP plumbing ─────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

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

if (azureAdEnabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();

if (azureAdEnabled)
{
    app.MapControllers();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Health endpoint — kept simple. OpenTelemetry / proper health checks
// arrive in a later slice.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>
/// Marker for WebApplicationFactory in integration tests.
/// </summary>
public partial class Program;
