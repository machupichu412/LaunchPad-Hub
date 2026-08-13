using FluentValidation;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Candidates;

/// <summary>
/// The self-service onboarding request. No CohortId — the caller's cohort is
/// resolved server-side (the single active cohort), never client-supplied.
/// SkillIds (not SkillNames) since the onboarding picker selects real Skill rows
/// rather than accepting free text — see SkillsController for how new skills get
/// into that list in the first place.
/// </summary>
public class CreateCandidateProfileRequest
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
    public int[] SkillIds { get; set; } = Array.Empty<int>();
}

public sealed class CreateCandidateProfileRequestValidator : AbstractValidator<CreateCandidateProfileRequest>
{
    public CreateCandidateProfileRequestValidator()
    {
        RuleFor(r => r.Location).MaximumLength(100);
        RuleFor(r => r.LinkedInUrl).MaximumLength(500);
        RuleFor(r => r.PortfolioUrl).MaximumLength(500);
        RuleFor(r => r.Bio).MaximumLength(1000);
        RuleFor(r => r.School).MaximumLength(200);
        RuleFor(r => r.Degree).MaximumLength(200);
        RuleFor(r => r.Gpa).InclusiveBetween(0m, 4m).When(r => r.Gpa.HasValue);
    }
}
