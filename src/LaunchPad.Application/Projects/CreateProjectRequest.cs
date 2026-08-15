using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Projects;

public class CreateProjectRequest
{
    public int CohortId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Availability AvailabilityNeeded { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int MaxCandidates { get; set; } = 1;
    public string[] RequiredSkillNames { get; set; } = Array.Empty<string>();
    public string[] PreferredSkillNames { get; set; } = Array.Empty<string>();
}
