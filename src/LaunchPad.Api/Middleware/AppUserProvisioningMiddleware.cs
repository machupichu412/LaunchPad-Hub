using System.Security.Claims;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Api.Middleware;

/// <summary>
/// Upserts the AppUser row on EntraObjectId for every authenticated request. This is
/// the only place AppUser rows are created — there is no separate user-management
/// screen, per CLAUDE.md ("never build a locally-managed... parallel user list").
/// </summary>
public sealed class AppUserProvisioningMiddleware
{
    private readonly RequestDelegate _next;
    public AppUserProvisioningMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, LaunchPadDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var oidClaim = context.User.FindFirstValue("oid");
            if (oidClaim is not null && Guid.TryParse(oidClaim, out var entraObjectId))
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
        }

        await _next(context);
    }
}
