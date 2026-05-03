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

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("oid", DevUserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, DevUserId.ToString()),
            new Claim(ClaimTypes.Name, DevUserName),
            new Claim(ClaimTypes.Email, DevUserEmail)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
