using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LaunchPad.Api.Authorization;

/// <summary>Editing, cancelling, or otherwise changing a project — see Policies.ChangeOwnProject.</summary>
public sealed class ChangeProjectRequirement : IAuthorizationRequirement
{
}

public sealed class ChangeProjectHandler : AuthorizationHandler<ChangeProjectRequirement, Project>
{
    private readonly ICurrentUser _user;
    public ChangeProjectHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ChangeProjectRequirement requirement,
        Project project)
    {
        // Ops runs the program and already approves and rejects projects, so it can correct
        // one. Executive reads, and is deliberately absent here (it is present in
        // OwnsProjectHandler, which guards the read endpoints).
        if (context.User.IsInRole(Roles.ProgramOps)
            || (context.User.IsInRole(Roles.Sponsor) && project.Sponsor.AppUser.EntraObjectId == _user.EntraObjectId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Speaking as the project's sponsor — see Policies.ProjectOwnerOnly.</summary>
public sealed class ProjectOwnerRequirement : IAuthorizationRequirement
{
}

public sealed class ProjectOwnerHandler : AuthorizationHandler<ProjectOwnerRequirement, Project>
{
    private readonly ICurrentUser _user;
    public ProjectOwnerHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectOwnerRequirement requirement,
        Project project)
    {
        // No role bypass at all, including Ops: this is identity, not authority. A user who
        // happens to hold both ProgramOps and Sponsor could otherwise file a sponsor's review
        // of a candidate on a project they have nothing to do with.
        if (context.User.IsInRole(Roles.Sponsor) && project.Sponsor.AppUser.EntraObjectId == _user.EntraObjectId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Changing an assignment's to-dos and deliverables — see Policies.ChangeOwnAssignment.</summary>
public sealed class ChangeAssignmentRequirement : IAuthorizationRequirement
{
}

public sealed class ChangeAssignmentHandler : AuthorizationHandler<ChangeAssignmentRequirement, Assignment>
{
    private readonly ICurrentUser _user;
    public ChangeAssignmentHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ChangeAssignmentRequirement requirement,
        Assignment assignment)
    {
        if (context.User.IsInRole(Roles.ProgramOps)
            || (context.User.IsInRole(Roles.Candidate) && assignment.Candidate.AppUser.EntraObjectId == _user.EntraObjectId)
            || (context.User.IsInRole(Roles.Sponsor) && assignment.Project.Sponsor.AppUser.EntraObjectId == _user.EntraObjectId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
