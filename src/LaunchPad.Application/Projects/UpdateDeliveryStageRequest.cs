using FluentValidation;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Projects;

/// <summary>Request for ProjectsController.AdvanceDeliveryStage. Forward-only-vs-any-value
/// enforcement lives in the controller (it needs to know who's asking), not here — this
/// validator only checks the shape of the request.</summary>
public class UpdateDeliveryStageRequest
{
    public ProjectDeliveryStage Stage { get; set; }
    public string? Reason { get; set; }
}

public sealed class UpdateDeliveryStageRequestValidator : AbstractValidator<UpdateDeliveryStageRequest>
{
    public UpdateDeliveryStageRequestValidator()
    {
        RuleFor(r => r.Stage).IsInEnum();
        RuleFor(r => r.Reason).MaximumLength(1000);
    }
}
