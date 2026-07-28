using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace LaunchPad.Api.Authorization;

public sealed class OwnsCandidateProfileRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// A Candidate may manage only their own profile. Ops and Exec bypass ownership.
/// </summary>
public sealed class OwnsCandidateProfileHandler : AuthorizationHandler<OwnsCandidateProfileRequirement, Candidate>
{
    private readonly ICurrentUser _user;
    public OwnsCandidateProfileHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnsCandidateProfileRequirement requirement,
        Candidate candidate)
    {
        if (context.User.IsInRole(Roles.ProgramOps) || context.User.IsInRole(Roles.Executive))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole(Roles.Candidate) && candidate.AppUser.EntraObjectId == _user.EntraObjectId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
