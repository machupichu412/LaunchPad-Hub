using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Candidates;

public static class CandidateStatusExtensions
{
    public static string ToOutcomeLabel(this CandidateStatus status) => status switch
    {
        CandidateStatus.InProgress => "In Progress",
        CandidateStatus.Hire => "Hire",
        CandidateStatus.TalentPlus => "Talent Plus",
        CandidateStatus.NoHire => "No Hire",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
