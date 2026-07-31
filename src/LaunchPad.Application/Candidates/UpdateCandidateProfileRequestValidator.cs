using FluentValidation;

namespace LaunchPad.Application.Candidates;

public sealed class UpdateCandidateProfileRequestValidator : AbstractValidator<UpdateCandidateProfileRequest>
{
    public UpdateCandidateProfileRequestValidator()
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
