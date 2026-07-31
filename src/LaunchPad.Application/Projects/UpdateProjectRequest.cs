using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Projects;

// Status/ApprovalStatus are intentionally absent — those transition through the
// Ops approval workflow (Phase 3), not a sponsor-initiated edit.
public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Availability AvailabilityNeeded { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string[] RequiredSkillNames { get; set; } = Array.Empty<string>();
    public string[] PreferredSkillNames { get; set; } = Array.Empty<string>();
}
