using FluentValidation;

namespace LaunchPad.Application.Projects;

public class RejectProjectRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class RejectProjectRequestValidator : AbstractValidator<RejectProjectRequest>
{
    public RejectProjectRequestValidator()
    {
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(1000);
    }
}
