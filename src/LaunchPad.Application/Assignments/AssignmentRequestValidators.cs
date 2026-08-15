using FluentValidation;

namespace LaunchPad.Application.Assignments;

public sealed class SubmitDeliverableRequestValidator : AbstractValidator<SubmitDeliverableRequest>
{
    // 100 MB — generous for a candidate deliverable; above the 4 MB Graph will need
    // GraphDocumentStorage's large-file upload-session path for, but well under anything
    // that would suggest a mistaken upload.
    public const long MaxDeliverableBytes = 100 * 1024 * 1024;

    public SubmitDeliverableRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.FileName).NotEmpty().MaximumLength(300);
        RuleFor(r => r.ContentLength)
            .GreaterThan(0).WithMessage("The selected file is empty.")
            .LessThanOrEqualTo(MaxDeliverableBytes).WithMessage("That file is too large — maximum size is 100 MB.");
    }
}

public sealed class UpdateTodoStatusRequestValidator : AbstractValidator<UpdateTodoStatusRequest>
{
    public UpdateTodoStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
    }
}

public sealed class CreateTodoRequestValidator : AbstractValidator<CreateTodoRequest>
{
    public CreateTodoRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Priority).IsInEnum();
    }
}
