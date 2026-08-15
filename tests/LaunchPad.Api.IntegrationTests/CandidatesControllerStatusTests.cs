using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// PATCH /api/candidates/{id}/status is the only write path for CandidateStatus outside
/// profile creation — see HireOutcomeRule for the suggestion this is meant to apply or
/// override. Ops-only; records an audit event same as every other status-changing action.
/// </summary>
public class CandidatesControllerStatusTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerStatusTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<int> SeedCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var program = new Domain.Entities.Program { Name = "Status Test Program" };
        var cohort = new Cohort { Program = program, Name = "Status Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "status-test@example.com", DisplayName = "Status Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };

        db.AddRange(program, cohort, candidate);
        await db.SaveChangesAsync();

        return candidate.CandidateId;
    }

    [Fact]
    public async Task UpdateStatus_AsProgramOps_ChangesStatus_AndRecordsAuditEvent()
    {
        var candidateId = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PatchAsJsonAsync(
            $"/api/candidates/{candidateId}/status",
            new UpdateCandidateStatusRequest { Status = CandidateStatus.Hire, Reason = "Final review supports conversion." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        dto!.Status.Should().Be(CandidateStatus.Hire);
        dto.Outcome.Should().Be("Hire");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var events = await db.AuditEvents.Where(e => e.EntityName == "Candidate" && e.EntityId == candidateId.ToString()).ToListAsync();
        events.Should().Contain(e => e.Action == "StatusChange" && e.Reason == "Final review supports conversion.");
    }

    [Fact]
    public async Task UpdateStatus_AsSponsor_IsForbidden()
    {
        var candidateId = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.PatchAsJsonAsync(
            $"/api/candidates/{candidateId}/status",
            new UpdateCandidateStatusRequest { Status = CandidateStatus.Hire, Reason = null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateStatus_ForUnknownCandidate_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PatchAsJsonAsync(
            "/api/candidates/999999/status",
            new UpdateCandidateStatusRequest { Status = CandidateStatus.Hire, Reason = null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
