using System.Security.Claims;
using LaunchPad.Application.Common;
using Microsoft.Identity.Web;

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
        //
        // GetObjectId() (Microsoft.Identity.Web), not a raw FindFirstValue("oid") —
        // a real Entra-issued token's object id can land under a different claim type
        // than the bare "oid" TestAuthHandler uses in tests (e.g. the long-form
        // http://schemas.microsoft.com/identity/claims/objectidentifier URI, depending
        // on JWT handler claim-mapping), which silently produced Guid.Empty here and
        // broke every "resolve my own row" lookup (candidate/sponsor creation, etc.)
        // against a real tenant despite roles working fine (role mapping is unaffected).
        var principal = accessor.HttpContext?.User;
        var oid = principal?.GetObjectId();

        EntraObjectId = oid is not null && Guid.TryParse(oid, out var parsed) ? parsed : Guid.Empty;
        Roles = principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
    }

    public bool IsInRole(string role) => Roles.Contains(role);
}
