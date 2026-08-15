using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Sponsors;

/// <summary>The sponsor's "My Candidates" roster row — candidates currently or previously
/// committed (OpsApproved/Active/Completed) to one of the sponsor's own projects.</summary>
public class SponsorCandidateDto
{
    public int AssignmentId { get; set; }
    public int CandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public AssignmentStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public string? SharePointFolderWebUrl { get; set; }
}
