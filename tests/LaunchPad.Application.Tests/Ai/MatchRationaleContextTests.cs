using FluentAssertions;
using LaunchPad.Application.Ai;
using Xunit;

namespace LaunchPad.Application.Tests.Ai;

public class MatchRationaleContextTests
{
    /// <summary>
    /// MatchRationaleContext is the redaction control for LLM-written rationales.
    ///
    /// A DTO mapper can gate a numeric field; it cannot redact a sentence. Once a model is
    /// writing the rationale, the only reliable way to keep a hidden rating out of prose that
    /// reaches Sponsors and Candidates is to never hand the model the number. So the shape of
    /// this record *is* the control, and widening it is a security change rather than a
    /// convenience — this test is here to make that visible in review.
    /// </summary>
    [Fact]
    public void ExposesACoarseBand_AndNoNumericRating()
    {
        var properties = typeof(MatchRationaleContext).GetProperties().Select(p => p.Name).ToList();

        properties.Should().Contain("PerformanceBand");
        properties.Should().NotContain(["PastPerformanceScore", "AverageScore", "OverallScore", "Gpa"]);

        typeof(MatchRationaleContext).GetProperty("PerformanceBand")!.PropertyType
            .Should().Be<string>("a band is a phrase — a number here would reach Sponsors as prose");
    }

    /// <summary>The candidate's name is deliberately absent too: the rationale explains a fit,
    /// and a model given a name will tend to write about the person rather than the match.</summary>
    [Fact]
    public void CarriesNoCandidateIdentity()
    {
        var properties = typeof(MatchRationaleContext).GetProperties().Select(p => p.Name).ToList();

        properties.Should().NotContain(["CandidateName", "DisplayName", "CandidateId", "Upn", "Email"]);
    }
}
