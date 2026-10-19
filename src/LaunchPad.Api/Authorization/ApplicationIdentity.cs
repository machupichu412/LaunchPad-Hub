using System.Security.Claims;
using LaunchPad.Application.Common;
using Microsoft.Identity.Web;

namespace LaunchPad.Api.Authorization;

public static class ApplicationIdentity
{
    public static bool IsValid(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse(user.GetObjectId(), out var oid) && oid != Guid.Empty
        && new[] { Roles.Executive, Roles.ProgramOps, Roles.Sponsor, Roles.Candidate, Roles.HiringManager, Roles.SubjectMatterExpert }
            .Any(user.IsInRole)
        && (user.FindFirst("scp") ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/scope"))?
            .Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("access_as_user") == true;
}
