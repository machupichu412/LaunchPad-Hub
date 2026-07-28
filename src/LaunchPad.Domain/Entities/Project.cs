using LaunchPad.Domain.Common;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

public class Project : Entity
{
    public int ProjectId { get; set; }
    public int CohortId { get; set; }
    public int SponsorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Availability AvailabilityNeeded { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectApprovalStatus ApprovalStatus { get; set; }
    public ProjectStatus Status { get; set; }

    public Cohort Cohort { get; set; } = null!;
    public Sponsor Sponsor { get; set; } = null!;
    public ICollection<ProjectSkill> Skills { get; set; } = new List<ProjectSkill>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
