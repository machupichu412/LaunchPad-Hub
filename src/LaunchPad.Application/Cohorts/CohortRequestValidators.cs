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

public sealed class ScheduleReviewsRequestValidator : AbstractValidator<ScheduleReviewsRequest>
{
    public ScheduleReviewsRequestValidator()
    {
        RuleFor(r => r.DueDate).NotEqual(default(DateOnly)).WithMessage("A due date is required.");
    }
}
