using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// GET /api/candidates/{id}/avatar — same ViewTalentPipeline gate as viewing the
/// candidate's other info (Get/GetByCohort), not a separate ownership check; a photo
/// carries none of the hidden-score sensitivity CLAUDE.md's redaction rule covers.
/// </summary>
public class CandidatesControllerAvatarTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerAvatarTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid CandidateOid, int CandidateId)> SeedCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var candidateOid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Avatar Test Program" };
        var cohort = new Cohort { Program = program, Name = "Avatar Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = candidateOid, Upn = $"{Guid.NewGuid()}@example.com", DisplayName = "Avatar Test Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };

        db.AddRange(program, cohort, candidate);
        await db.SaveChangesAsync();

        return (candidateOid, candidate.CandidateId);
    }

    [Fact]
    public async Task GetAvatar_WhenCandidateHasNoPhoto_ReturnsNotFound()
    {
        var (_, candidateId) = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.GetAsync($"/api/candidates/{candidateId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvatar_AfterTheCandidateUploadsOne_ReturnsItToAnAuthorizedViewer()
    {
        var (candidateOid, candidateId) = await SeedCandidateAsync();

        var candidateClient = _factory.CreateClient();
        candidateClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        candidateClient.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, candidateOid.ToString());

        var imageBytes = new byte[] { 7, 7, 7 };
        var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var uploadResponse = await candidateClient.PostAsync("/api/me/avatar", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var opsClient = _factory.CreateClient();
        opsClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await opsClient.GetAsync($"/api/candidates/{candidateId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(imageBytes);
    }

    [Fact]
    public async Task GetAvatar_AsCandidateRole_IsForbidden()
    {
        var (_, candidateId) = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.GetAsync($"/api/candidates/{candidateId}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
