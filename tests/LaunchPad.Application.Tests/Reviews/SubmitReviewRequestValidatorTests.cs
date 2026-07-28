using FluentAssertions;
using LaunchPad.Application.Reviews;
using Xunit;

namespace LaunchPad.Application.Tests.Reviews;

public class SubmitReviewRequestValidatorTests
{
    private readonly SubmitReviewRequestValidator _sut = new();

    [Fact]
    public void Validate_RejectsRequestWithNoRatingDimensions()
    {
        var request = new SubmitReviewRequest { AssignmentId = 1 };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsOutOfRangeScore()
    {
        var request = new SubmitReviewRequest { AssignmentId = 1, Commitment = 6 };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsPartialValidRequest()
    {
        var request = new SubmitReviewRequest { AssignmentId = 1, Commitment = 4 };

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
