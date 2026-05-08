using Loom.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loom.Web.Controllers;

/// <summary>
/// Demo-only impersonation surface. Sets a cookie that
/// <see cref="DevAuthenticationHandler"/> reads on every request to
/// emit a different principal. Disappears alongside the dev auth
/// handler once real auth lands.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("demo")]
public sealed class ImpersonationController : ControllerBase
{
    [HttpPost("impersonate")]
    public IActionResult SetUser([FromForm] Guid userId, [FromForm] string? returnUrl = null)
    {
        if (DevAuthenticationHandler.FindDemoUser(userId) is not null)
        {
            Response.Cookies.Append(
                DevAuthenticationHandler.ImpersonationCookieName,
                userId.ToString(),
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    // Explicitly long-lived so a refresh keeps the persona.
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    IsEssential = true,
                    Path = "/"
                });
        }
        var safeReturn = string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl) ? "/" : returnUrl;
        return LocalRedirect(safeReturn);
    }
}
