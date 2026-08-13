using System.Security.Claims;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

namespace LaunchPad.Api.Middleware;

/// <summary>
/// Upserts the AppUser row on EntraObjectId for every authenticated request. This is
/// the only place AppUser rows are created — there is no separate user-management
/// screen, per CLAUDE.md ("never build a locally-managed... parallel user list").
/// </summary>
public sealed class AppUserProvisioningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AppUserProvisioningMiddleware> _logger;

    public AppUserProvisioningMiddleware(RequestDelegate next, ILogger<AppUserProvisioningMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, LaunchPadDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // GetObjectId() (Microsoft.Identity.Web), not a raw FindFirstValue("oid") —
            // see the matching note in CurrentUser.cs. A mismatch here means this
            // block silently never provisions a row at all, which then surfaces much
            // later as a confusing "your account isn't provisioned yet" from whichever
            // endpoint tried to resolve the caller's own row — hence the warning below.
            var oid = context.User.GetObjectId();
            if (oid is not null && Guid.TryParse(oid, out var entraObjectId))
            {
                var exists = await db.AppUsers.AnyAsync(u => u.EntraObjectId == entraObjectId);
                if (!exists)
                {
                    db.AppUsers.Add(new AppUser
                    {
                        EntraObjectId = entraObjectId,
                        Upn = context.User.FindFirstValue(ClaimTypes.Upn)
                            ?? context.User.FindFirstValue("preferred_username")
                            ?? string.Empty,
                        DisplayName = context.User.FindFirstValue("name") ?? string.Empty
                    });
                    await db.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogWarning(
                    "Authenticated request had no resolvable object id claim — AppUser was not provisioned. ClaimTypes present: {ClaimTypes}",
                    string.Join(", ", context.User.Claims.Select(c => c.Type).Distinct()));
            }
        }

        await _next(context);
    }
}
