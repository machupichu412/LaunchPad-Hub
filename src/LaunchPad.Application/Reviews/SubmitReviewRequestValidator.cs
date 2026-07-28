using FluentValidation;

namespace LaunchPad.Application.Reviews;

public sealed class SubmitReviewRequestValidator : AbstractValidator<SubmitReviewRequest>
{
    public SubmitReviewRequestValidator()
    {
        RuleFor(r => r.AssignmentId).GreaterThan(0);

        RuleFor(r => r.Commitment).InclusiveBetween((byte)1, (byte)5).When(r => r.Commitment.HasValue);
        RuleFor(r => r.Availability).InclusiveBetween((byte)1, (byte)5).When(r => r.Availability.HasValue);
        RuleFor(r => r.Guidance).InclusiveBetween((byte)1, (byte)5).When(r => r.Guidance.HasValue);
        RuleFor(r => r.OutputQuality).InclusiveBetween((byte)1, (byte)5).When(r => r.OutputQuality.HasValue);

        RuleFor(r => r)
            .Must(r => r.Commitment.HasValue || r.Availability.HasValue || r.Guidance.HasValue || r.OutputQuality.HasValue)
            .WithMessage("At least one rating dimension must be provided.");
    }
}
