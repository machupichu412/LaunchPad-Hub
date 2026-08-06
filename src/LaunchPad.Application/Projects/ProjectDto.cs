using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Projects;

public class ProjectDto
{
    public int ProjectId { get; set; }
    public int CohortId { get; set; }
    public int SponsorId { get; set; }
    public string SponsorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Availability AvailabilityNeeded { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectApprovalStatus ApprovalStatus { get; set; }
    public ProjectStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public string SponsorTeamsLink { get; set; } = string.Empty;

    /// <summary>The requesting candidate's own 1-5 rating for this project, or null if
    /// they haven't rated it. Only ever populated on candidate-facing actions — see
    /// ProjectsController.GetOpenProjects/GetOpenDetail.</summary>
    public byte? MyInterestRating { get; set; }

    public IReadOnlyList<ProjectSkillDto> RequiredSkills { get; set; } = Array.Empty<ProjectSkillDto>();
}

public class ProjectSkillDto
{
    public string SkillName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
