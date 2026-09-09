using FluentAssertions;
using LaunchPad.Application.Ai;
using LaunchPad.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Exercises the NoOp AI clients directly — same reasoning as CompositeNotificationPublisherTests,
/// which also lives here rather than in LaunchPad.Application.Tests: that project references only
/// LaunchPad.Application, deliberately, and these implementations live in Infrastructure.
///
/// These are what every host resolves while AzureOpenAI:Endpoint is blank, so this is the
/// configuration the local demo and the whole integration suite actually run in. Their contract
/// is to return each interface's documented "unavailable" value rather than throw — callers must
/// degrade, not fail.
///
/// Worth pinning precisely because it looks trivial. A NoOp that threw would turn "AI is off"
/// into a failed matching run; one that returned an empty ResumeExtraction instead of null would
/// tell a candidate their resume contained no skills.
/// </summary>
public class NoOpAiClientTests
{
    [Fact]
    public async Task ResumeExtraction_ReturnsNull_NotAnEmptyExtraction()
    {
        var sut = new NoOpResumeExtractionClient(NullLogger<NoOpResumeExtractionClient>.Instance);

        var result = await sut.ExtractAsync("Five years building payment systems.", ["React"]);

        // Null is "no result". An empty ResumeExtraction would mean "read it, found nothing",
        // which is a different claim and a much worse thing to show someone.
        result.Should().BeNull();
    }

    [Fact]
    public async Task Embedding_ReturnsEmpty_SoCallersFallBackToNeutralSimilarity()
    {
        var sut = new NoOpEmbeddingClient(NullLogger<NoOpEmbeddingClient>.Instance);

        (await sut.EmbedAsync(["one", "two"])).Should().BeEmpty();
        (await sut.EmbedAsync([])).Should().BeEmpty();
    }

    [Fact]
    public void Embedding_ModelName_IsNotAPlausibleModelName()
    {
        // The model name is part of the embedding cache key. A plausible-looking name here
        // would let no-op runs write cache rows that appear to hold real vectors.
        new NoOpEmbeddingClient(NullLogger<NoOpEmbeddingClient>.Instance)
            .ModelName.Should().Be("none");
    }

    [Fact]
    public async Task MatchRationaleWriter_ReturnsNull_SoTheDeterministicRationaleSurvives()
    {
        var sut = new NoOpMatchRationaleWriter(NullLogger<NoOpMatchRationaleWriter>.Instance);

        var context = new MatchRationaleContext(
            ProjectName: "Supply Chain Dashboard",
            ProjectDescription: "Build an operations dashboard.",
            RequiredSkillsMatched: ["Power BI"],
            RequiredSkillsMissing: ["SQL"],
            PreferredSkillsMatched: [],
            PerformanceBand: "strong prior performance",
            GraduationAlignment: "on track to graduate on or after the project ends",
            CandidateInterestRating: 4);

        (await sut.WriteAsync(context)).Should().BeNull();
    }

    [Fact]
    public async Task NoneOfThemThrow_OnDegenerateInput()
    {
        var extraction = new NoOpResumeExtractionClient(NullLogger<NoOpResumeExtractionClient>.Instance);
        var embedding = new NoOpEmbeddingClient(NullLogger<NoOpEmbeddingClient>.Instance);

        var act = async () =>
        {
            await extraction.ExtractAsync(string.Empty, []);
            await embedding.EmbedAsync([string.Empty]);
        };

        await act.Should().NotThrowAsync();
    }

}

/// <summary>
/// Proves the three AI interfaces actually resolve from the running API host's container, and
/// resolve to the NoOp implementations while AzureOpenAI:Endpoint is blank.
///
/// Nothing in the app consumes these yet, so a typo'd or missing registration would otherwise sit
/// undetected until the first real caller landed — at which point the failure would look like a
/// bug in that caller rather than in the wiring.
/// </summary>
public class AiClientRegistrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AiClientRegistrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void AiInterfaces_ResolveToNoOpImplementations_WhenNoEndpointConfigured()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IResumeExtractionClient>().Should().BeOfType<NoOpResumeExtractionClient>();
        sp.GetRequiredService<IEmbeddingClient>().Should().BeOfType<NoOpEmbeddingClient>();
        sp.GetRequiredService<IMatchRationaleWriter>().Should().BeOfType<NoOpMatchRationaleWriter>();
    }
}
