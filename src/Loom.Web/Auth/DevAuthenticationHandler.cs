using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Loom.Web.Auth;

/// <summary>
/// Always-succeed authentication handler for local development. Emits a
/// synthetic ClaimsPrincipal with a stable subject id, so [Authorize]-marked
/// pages render without a real Entra tenant. Wired into the pipeline only
/// when the configuration flag AzureAd:Enabled is false.
///
/// NEVER register this in any environment that holds real data — there is
/// no actual authentication.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    /// <summary>
    /// Stable dev-user identity — pages can read this oid via the
    /// CurrentUserAccessor and use it for ownership checks.
    /// </summary>
    public static readonly Guid DevUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const string DevUserEmail = "dev@loom.local";
    public const string DevUserName = "Dev User";

    /// <summary>
    /// Cookie name for the demo "view as" picker. Holds the GUID of the
    /// impersonated user. Read on every request; if set, the handler
    /// issues a principal for that user instead of the default dev user.
    /// Demo-only — disappears once real auth lands.
    /// </summary>
    public const string ImpersonationCookieName = "loom_demo_user";

    /// <summary>
    /// Hardcoded roster of demo personas. Mirrors the worked example in
    /// the team walkthrough so reviewers can switch between PO / UX /
    /// developer / tech-reviewer hats and see what each role sees.
    /// </summary>
    public static readonly IReadOnlyList<DemoUser> DemoUsers =
    [
        new(DevUserId, "Dev User", "PO", "dev@loom.local"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Andy (PO)", "PO", "andy@loom.local"),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Adam (UX)", "UX", "adam@loom.local"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Marcus (Dev)", "Dev", "marcus@loom.local"),
        new(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Tara (Tech reviewer)", "Lead", "tara@loom.local")
    ];

    public static DemoUser? FindDemoUser(Guid id) =>
        DemoUsers.FirstOrDefault(u => u.Id == id);

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var resolved = ResolveActiveUser();
        var claims = new[]
        {
            new Claim("oid", resolved.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, resolved.Id.ToString()),
            new Claim(ClaimTypes.Name, resolved.Name),
            new Claim(ClaimTypes.Email, resolved.Email),
            new Claim("loom_role", resolved.Role)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private DemoUser ResolveActiveUser()
    {
        var raw = Request.Cookies[ImpersonationCookieName];
        if (Guid.TryParse(raw, out var id) && FindDemoUser(id) is { } u)
        {
            return u;
        }
        return DemoUsers[0]; // default = original dev user
    }
}

public sealed record DemoUser(Guid Id, string Name, string Role, string Email);
