using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

/// <summary>
/// The caller's own assignment — only ever returned for the authenticated candidate's
/// own record (see AssignmentsController.GetMine). It carries the match rationale but no
/// match score: candidates and sponsors never receive a numeric score (launchpad-build-guide
/// §5.3), and "matched 1/1 required skills" is the part that actually explains the match.
/// Ops and Exec still see the number, on PendingAssignmentDto and AssignmentDto.
/// </summary>
public class MyAssignmentDto
{
    public int AssignmentId { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
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
