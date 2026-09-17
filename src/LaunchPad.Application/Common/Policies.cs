namespace LaunchPad.Application.Common;

public static class Policies
{
    public const string ViewTalentPipeline = nameof(ViewTalentPipeline);
    public const string ViewHiddenScores = nameof(ViewHiddenScores);
    public const string ApproveMatch = nameof(ApproveMatch);
    public const string ApproveProject = nameof(ApproveProject);
    public const string ManageOwnProfile = nameof(ManageOwnProfile);
    /// <summary>Reaching a project's own pages: the owning Sponsor, plus Ops and Executive,
    /// who see everything.</summary>
    public const string ManageOwnProject = nameof(ManageOwnProject);

    /// <summary>Changing a project: the owning Sponsor and Ops. Executive is a reporting role
    /// and every other Ops-only action already refuses it, so it does not edit or cancel
    /// somebody else's project either.</summary>
    public const string ChangeOwnProject = nameof(ChangeOwnProject);

    /// <summary>Acting *as* the project's sponsor — submitting the sponsor's review of a
    /// candidate. No role bypasses this one: a reviewer who isn't the sponsor would file a
    /// review the candidate reads as their sponsor's.</summary>
    public const string ProjectOwnerOnly = nameof(ProjectOwnerOnly);

    public const string ManageOwnAssignment = nameof(ManageOwnAssignment);

    /// <summary>Changing an assignment's to-dos and deliverables: the people doing the work
    /// (candidate), the sponsor who owns the project, and Ops. Same Executive reasoning as
    /// ChangeOwnProject.</summary>
    public const string ChangeOwnAssignment = nameof(ChangeOwnAssignment);
    public const string ViewOwnAssignment = nameof(ViewOwnAssignment);
}
