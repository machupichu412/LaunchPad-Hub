using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

/// <summary>
/// The caller's own assignment — only ever returned for the authenticated candidate's
/// own record (see AssignmentsController.GetMine), so unlike the generic AssignmentDto,
/// MatchScore is safe to include unconditionally: ownership itself is the authorization
/// gate, not a role check. This is an algorithmic fit score, not a hire-quality
/// judgment, so it's a different kind of "score" than Review.OverallScore.
/// </summary>
public class MyAssignmentDto
{
    public int AssignmentId { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? MatchScore { get; set; }
    public string? MatchRationale { get; set; }

    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectDescription { get; set; }
    public string[] ProjectSkills { get; set; } = Array.Empty<string>();
    public string SponsorName { get; set; } = string.Empty;
    public string? SponsorOrganization { get; set; }

    public int TasksTotal { get; set; }
    public int TasksComplete { get; set; }
}
