using FluentValidation;

namespace LaunchPad.Application.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(r => r.CohortId).GreaterThan(0);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(300);
        RuleFor(r => r.EndDate)
            .GreaterThanOrEqualTo(r => r.StartDate)
            .When(r => r.StartDate.HasValue && r.EndDate.HasValue)
            .WithMessage("EndDate must not be before StartDate.");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(300);
        RuleFor(r => r.EndDate)
            .GreaterThanOrEqualTo(r => r.StartDate)
            .When(r => r.StartDate.HasValue && r.EndDate.HasValue)
            .WithMessage("EndDate must not be before StartDate.");
    }
}
