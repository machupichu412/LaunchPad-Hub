using FluentValidation;

namespace LaunchPad.Application.Matching;

public class RateInterestRequest
{
    public byte Rating { get; set; }
}

public sealed class RateInterestRequestValidator : AbstractValidator<RateInterestRequest>
{
    public RateInterestRequestValidator()
    {
        RuleFor(r => r.Rating).InclusiveBetween((byte)1, (byte)5);
    }
}
