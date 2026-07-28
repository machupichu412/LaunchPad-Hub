namespace LaunchPad.Application.Common;

/// <summary>
/// Values of the "roles" claim as declared on the LaunchPad-API app registration's
/// appRoles manifest. Must match exactly — see launchpad-build-guide.md §6.2.
/// </summary>
public static class Roles
{
    public const string Executive = "LaunchPad.Executive";
    public const string ProgramOps = "LaunchPad.ProgramOps";
    public const string Sponsor = "LaunchPad.Sponsor";
    public const string Candidate = "LaunchPad.Candidate";
    public const string HiringManager = "LaunchPad.HiringManager";
}
