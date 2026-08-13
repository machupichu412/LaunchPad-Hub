using FluentValidation;

namespace LaunchPad.Application.Sponsors;

/// <summary>
/// The self-service onboarding request, mirroring CreateCandidateProfileRequest.
/// No CohortId — unlike Candidate, Sponsor isn't cohort-scoped at all (only the
/// projects a Sponsor creates are); see Sponsor.cs / Project.CohortId.
/// </summary>
public class CreateSponsorProfileRequest
{
    public string? Organization { get; set; }
    public string? Title { get; set; }
}

public sealed class CreateSponsorProfileRequestValidator : AbstractValidator<CreateSponsorProfileRequest>
{
    public CreateSponsorProfileRequestValidator()
    {
        RuleFor(r => r.Organization).MaximumLength(200);
        RuleFor(r => r.Title).MaximumLength(200);
    }
}
