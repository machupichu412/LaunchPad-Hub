using FluentValidation;

namespace LaunchPad.Application.Assignments;

public sealed class CreateDeliverableRequestValidator : AbstractValidator<CreateDeliverableRequest>
{
    public CreateDeliverableRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.FileName).NotEmpty().MaximumLength(300);
    }
}

public sealed class UpdateTodoStatusRequestValidator : AbstractValidator<UpdateTodoStatusRequest>
{
    public UpdateTodoStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
    }
}
