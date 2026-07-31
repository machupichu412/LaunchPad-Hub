using FluentValidation;

namespace LaunchPad.Application.Cohorts;

public sealed class CreateCohortRequestValidator : AbstractValidator<CreateCohortRequest>
{
    public CreateCohortRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.EndDate).GreaterThanOrEqualTo(r => r.StartDate)
            .WithMessage("EndDate must not be before StartDate.");
    }
}
