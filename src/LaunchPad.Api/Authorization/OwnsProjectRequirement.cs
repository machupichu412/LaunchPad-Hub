using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LaunchPad.Api.Authorization;

public sealed class OwnsProjectRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Role alone is insufficient: a Sponsor may manage their own project only. Ops and
/// Exec bypass ownership entirely. See launchpad-build-guide.md §5.2.
/// </summary>
public sealed class OwnsProjectHandler : AuthorizationHandler<OwnsProjectRequirement, Project>
{
    private readonly ICurrentUser _user;
    public OwnsProjectHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnsProjectRequirement requirement,
        Project project)
    {
        if (context.User.IsInRole(Roles.ProgramOps) || context.User.IsInRole(Roles.Executive))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole(Roles.Sponsor) && project.Sponsor.AppUser.EntraObjectId == _user.EntraObjectId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
