using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LaunchPad.Api.Authorization;

public sealed class OwnsAssignmentRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// A Candidate may only reach their own assignment's todos/deliverables/evaluations.
/// Ops and Exec bypass ownership, same shape as OwnsProjectHandler.
/// </summary>
public sealed class OwnsAssignmentHandler : AuthorizationHandler<OwnsAssignmentRequirement, Assignment>
{
    private readonly ICurrentUser _user;
    public OwnsAssignmentHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnsAssignmentRequirement requirement,
        Assignment assignment)
    {
        if (context.User.IsInRole(Roles.ProgramOps) || context.User.IsInRole(Roles.Executive))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole(Roles.Candidate) && assignment.Candidate.AppUser.EntraObjectId == _user.EntraObjectId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
