using FluentValidation;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Candidates;

/// <summary>Ops applies or overrides a candidate's hire outcome — see
/// CandidatesController.UpdateStatus and HireOutcomeRule, whose suggestion this request
/// is meant to act on (though Ops can set any status regardless of what was suggested).</summary>
public class UpdateCandidateStatusRequest
{
    public CandidateStatus Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class UpdateCandidateStatusRequestValidator : AbstractValidator<UpdateCandidateStatusRequest>
{
    public UpdateCandidateStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.Reason).MaximumLength(1000);
    }
}
