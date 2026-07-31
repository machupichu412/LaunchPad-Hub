using LaunchPad.Domain.Common;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

public class Candidate : Entity
{
    public int CandidateId { get; set; }
    public int AppUserId { get; set; }
    public int CohortId { get; set; }
    public string? Location { get; set; }
    public Availability Availability { get; set; }
    public DateOnly? GraduationDate { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? ResumeBlobPath { get; set; }
    public CandidateStatus Status { get; set; }
    public string? Bio { get; set; }
    public string? School { get; set; }
    public string? Degree { get; set; }
    public decimal? Gpa { get; set; }

    public AppUser AppUser { get; set; } = null!;
    public Cohort Cohort { get; set; } = null!;
    public ICollection<CandidateSkill> Skills { get; set; } = new List<CandidateSkill>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
