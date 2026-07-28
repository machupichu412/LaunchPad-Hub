using System.Security.Claims;
using LaunchPad.Application.Common;

namespace LaunchPad.Api.Authorization;

public sealed class CurrentUser : ICurrentUser
{
    public Guid EntraObjectId { get; }
    public string[] Roles { get; }

    public CurrentUser(IHttpContextAccessor accessor)
    {
        // ASP.NET Core's default IAuthorizationHandlerProvider resolves every
        // registered IAuthorizationHandler for every authorization check, regardless
        // of which policy is being evaluated — so this constructs even for anonymous
        // requests. It must not throw here; ownership checks that actually need a
        // real EntraObjectId only run once a caller has already cleared role checks.
        var principal = accessor.HttpContext?.User;
        var oidClaim = principal?.FindFirstValue("oid");

        EntraObjectId = oidClaim is not null && Guid.TryParse(oidClaim, out var parsed) ? parsed : Guid.Empty;
        Roles = principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
    }

    public bool IsInRole(string role) => Roles.Contains(role);
}
