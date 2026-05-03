using System.Security.Claims;
using Loom.Web.Auth;

namespace Loom.Web.Services;

/// <summary>
/// Reads the current user's stable Entra object id (oid) from the active
/// HttpContext / SignalR connection. Falls back to the dev user when
/// AzureAd:Enabled=false. Application services consume Guid identities;
/// this is the boundary that turns a ClaimsPrincipal into one.
/// </summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
{
    public Guid GetUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (TryGetUserId(principal, out var id))
        {
            return id;
        }
        // Authoring/preview render or unauthenticated context: return the
        // dev sentinel. Production-mode pages are [Authorize]-gated so this
        // path is never reached when Entra is configured.
        return DevAuthenticationHandler.DevUserId;
    }

    public string GetDisplayName()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.FindFirst(ClaimTypes.Name)?.Value
            ?? principal?.Identity?.Name
            ?? DevAuthenticationHandler.DevUserName;
    }

    internal static bool TryGetUserId(ClaimsPrincipal? principal, out Guid id)
    {
        var raw = principal?.FindFirst("oid")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(raw, out id))
        {
            return true;
        }
        id = Guid.Empty;
        return false;
    }
}
