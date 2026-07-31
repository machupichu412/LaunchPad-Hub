using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Candidates;

public class UpdateCandidateProfileRequest
{
    public string? Location { get; set; }
    public Availability Availability { get; set; }
    public DateOnly? GraduationDate { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? Bio { get; set; }
    public string? School { get; set; }
    public string? Degree { get; set; }
    public decimal? Gpa { get; set; }
    public string[] SkillNames { get; set; } = Array.Empty<string>();
}
