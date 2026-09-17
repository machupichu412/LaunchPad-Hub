using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// The list endpoints resolve risk and hire-outcome signals in batch, while GET
/// /api/candidates/{id} resolves them one at a time. Two code paths producing one DTO is
/// exactly the shape that drifts silently, so this pins them together: whatever the list
/// says about a candidate, the detail endpoint must say too.
/// </summary>
public class CandidatesControllerBatchParityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerBatchParityTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(int CohortId, int CandidateId)> SeedCandidateWithFinalReviewAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Batch Parity Program" };
        var cohort = new Cohort
        {
            Program = program,
            Name = "Batch Parity Cohort",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 1),
            Status = CohortStatus.Active,
        };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"{Guid.NewGuid()}@example.com", DisplayName = "Batch Parity Candidate" },
            Availability = Availability.FullTime,
            Status = CandidateStatus.InProgress,
        };

        db.AddRange(program, cohort, candidate);
        await db.SaveChangesAsync();

        return (cohort.CohortId, candidate.CandidateId);
    }

    [Fact]
    public async Task ListAndDetail_ProduceTheSameDtoForTheSameCandidate()
    {
        var (cohortId, candidateId) = await SeedCandidateWithFinalReviewAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var fromList = (await client.GetFromJsonAsync<List<CandidateDto>>(
                $"/api/candidates/cohort/{cohortId}", TestJsonOptions.Default))!
            .Single(c => c.CandidateId == candidateId);

        var fromDetail = await client.GetFromJsonAsync<CandidateDto>(
            $"/api/candidates/{candidateId}", TestJsonOptions.Default);

        // Serialized comparison rather than field-by-field, so a DTO gaining a field that
        // only one of the two paths populates fails here instead of shipping.
        JsonSerializer.Serialize(fromList, TestJsonOptions.Default)
            .Should().Be(JsonSerializer.Serialize(fromDetail, TestJsonOptions.Default));
    }

    [Fact]
    public async Task List_WithNoCandidates_DoesNotQueryAndReturnsEmpty()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var program = new Domain.Entities.Program { Name = "Empty Cohort Program" };
        var emptyCohort = new Cohort
        {
            Program = program,
            Name = "Empty Cohort",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 1),
            Status = CohortStatus.Active,
        };
        db.AddRange(program, emptyCohort);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var candidates = await client.GetFromJsonAsync<List<CandidateDto>>(
            $"/api/candidates/cohort/{emptyCohort.CohortId}", TestJsonOptions.Default);

        candidates.Should().BeEmpty();
    }
}
