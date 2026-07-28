using LaunchPad.Domain.Common;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// The stateful join between Project and Candidate. A unique filtered index in the
/// database (Status IN (OpsApproved, Active)) enforces one active assignment per
/// candidate — never bypass it with raw inserts.
/// </summary>
public class Assignment : Entity
{
    public int AssignmentId { get; set; }
    public int ProjectId { get; set; }
    public int CandidateId { get; set; }
    public decimal? MatchScore { get; set; }
    public string? MatchRationale { get; set; }
    public DateTime? SponsorApprovedUtc { get; set; }
    public int? SponsorApprovedBy { get; set; }
    public DateTime? OpsApprovedUtc { get; set; }
    public int? OpsApprovedBy { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public Project Project { get; set; } = null!;
    public Candidate Candidate { get; set; } = null!;
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<ProjectTodo> Todos { get; set; } = new List<ProjectTodo>();
}
