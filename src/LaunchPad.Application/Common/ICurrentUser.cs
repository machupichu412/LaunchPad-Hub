namespace LaunchPad.Application.Common;

/// <summary>
/// Bridges the Entra token (identity + roles) to the request. Does not resolve
/// which Candidate/Sponsor row the caller is — that lookup is the responsibility
/// of resource-based authorization handlers, not this service.
/// </summary>
public interface ICurrentUser
{
    Guid EntraObjectId { get; }
    string[] Roles { get; }
    bool IsInRole(string role);
}
