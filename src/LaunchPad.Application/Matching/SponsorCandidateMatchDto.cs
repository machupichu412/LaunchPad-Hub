using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Matching;

/// <summary>
/// A sponsor's eligible-candidate browsing view for one project. Deliberately a separate
/// type from CandidateDto — it structurally cannot carry AverageScore/HasPerformanceRisk/
/// HasEngagementRisk, so a sponsor is incapable of receiving those hidden fields regardless
/// of any future role-check bug (stronger guarantee than the additive-mapper pattern used
/// for CandidateDto — see CLAUDE.md's "hidden ratings" rule).
/// </summary>
public class SponsorCandidateMatchDto
{
    public int CandidateId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Availability Availability { get; set; }
    public DateOnly? GraduationDate { get; set; }
    public string? Bio { get; set; }
    public string? School { get; set; }
    public string? Degree { get; set; }
    public decimal? Gpa { get; set; }
    public string[] Skills { get; set; } = Array.Empty<string>();
    public decimal Score { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public byte? InterestRating { get; set; }

    /// <summary>True if this candidate has a Proposed/SponsorApproved assignment on a
    /// different project — not blocking, just a heads-up badge (see CLAUDE.md-adjacent
    /// design note: the engine already lets sponsors compete for the same proposed
    /// candidate, arbitrated at Ops-approval time).</summary>
    public bool HasPendingAssignmentElsewhere { get; set; }

    /// <summary>Non-null if Program Ops's cohort-wide batch matching already proposed this
    /// candidate for THIS project (Assignment.Status == Proposed) — the id of that Assignment,
    /// for the gallery to act on directly via the existing recommend/reject endpoints instead
    /// of issuing a fresh request that would create a duplicate Assignment row.</summary>
    public int? ProposedAssignmentId { get; set; }
}
