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
/// Proves GET/PUT /api/candidates/me resolve strictly from the caller's own
/// EntraObjectId — there is no candidateId in the route, so there is nothing for a
/// malicious caller to substitute to reach someone else's profile.
/// </summary>
public class CandidatesControllerProfileTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CandidatesControllerProfileTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<Guid> SeedCandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var oid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = "Test Program" };
        var cohort = new Cohort { Program = program, Name = "Test Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = oid, Upn = "candidate@example.com", DisplayName = "Test Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
        };

        // Skill names on a profile now have to exist already (they are no longer created on
        // the fly), so the taxonomy this fixture's tests select from is seeded here.
        var category = new SkillCategory { Name = $"Category {oid}" };
        db.AddRange(program, cohort, candidate, category);
        foreach (var name in new[] { "React", "TypeScript" })
        {
            if (!await db.Skills.AnyAsync(s => s.Name == name))
            {
                db.Add(new Skill { Name = name, SkillCategory = category });
            }
        }
        await db.SaveChangesAsync();

        return oid;
    }

    [Fact]
    public async Task GetMe_WithNoCandidateRecord_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/candidates/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMe_ResolvesOwnRecord()
    {
        var oid = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.GetAsync("/api/candidates/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        dto!.DisplayName.Should().Be("Test Candidate");
    }

    [Fact]
    public async Task UpdateMe_PersistsChanges_AndOnlyAffectsOwnRecord()
    {
        var oid = await SeedCandidateAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var request = new UpdateCandidateProfileRequest
        {
            Location = "Austin, TX",
            Availability = Availability.FullTime,
            SkillNames = new[] { "React", "TypeScript" },
        };
        var response = await client.PutAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CandidateDto>(TestJsonOptions.Default);
        dto!.Location.Should().Be("Austin, TX");
        dto.Skills.Should().Contain(new[] { "React", "TypeScript" });
    }

    /// <summary>Seeds a candidate who already holds one skill with a given provenance —
    /// the setup a plain profile save must not disturb. Skill names are caller-supplied and
    /// must be unique per test: Skill.Name carries a unique index and this fixture's database
    /// is shared across every test in the class.</summary>
    private async Task<(Guid Oid, int SkillId)> SeedCandidateWithSkillAsync(
        string skillName, SkillSource source, byte proficiency)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var oid = Guid.NewGuid();
        var program = new Domain.Entities.Program { Name = $"Program {oid}" };
        var cohort = new Cohort { Program = program, Name = $"Cohort {oid}", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var category = new SkillCategory { Name = $"Category {oid}" };
        // Reused if an earlier test in this class already seeded it: names are resolved
        // against the taxonomy now, so two rows sharing a name would both match and the
        // candidate would end up holding the same skill twice.
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Name == skillName)
            ?? new Skill { Name = skillName, SkillCategory = category };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = oid, Upn = $"{oid}@example.com", DisplayName = "Test Candidate" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
            Skills = new List<CandidateSkill>
            {
                new() { Skill = skill, Source = source, Proficiency = proficiency },
            },
        };

        db.AddRange(program, cohort, candidate);
        if (skill.SkillId == 0) db.Add(skill);
        // The other names each provenance test swaps in must exist too.
        foreach (var name in new[] { "Terraform (provenance)", "Rust (provenance)", "Elixir (provenance)", "Go (provenance)", "Kubernetes (provenance)" })
        {
            if (name != skillName && !await db.Skills.AnyAsync(s => s.Name == name))
            {
                db.Add(new Skill { Name = name, SkillCategory = category });
            }
        }
        await db.SaveChangesAsync();

        return (oid, skill.SkillId);
    }

    private HttpClient CandidateClient(Guid oid)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());
        return client;
    }

    private async Task<CandidateSkill?> ReadCandidateSkillAsync(Guid oid, int skillId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var candidate = await db.Candidates
            .Include(c => c.AppUser)
            .Include(c => c.Skills)
            .FirstAsync(c => c.AppUser.EntraObjectId == oid);
        return candidate.Skills.SingleOrDefault(cs => cs.SkillId == skillId);
    }

    /// <summary>
    /// The regression that matters: UpdateMe used to assign a fresh Skills collection, which
    /// made EF delete every tracked CandidateSkill and re-insert it as SelfReported. Ops-verified
    /// provenance was being destroyed on every ordinary profile save. Resume-parsed skills would
    /// have been wiped the same way.
    /// </summary>
    [Fact]
    public async Task UpdateMe_KeepingAnExistingSkill_PreservesItsSourceAndProficiency()
    {
        var (oid, skillId) = await SeedCandidateWithSkillAsync("Kubernetes (provenance)", SkillSource.OpsVerified, 5);
        var client = CandidateClient(oid);

        var request = new UpdateCandidateProfileRequest
        {
            Location = "Austin, TX",
            Availability = Availability.FullTime,
            // The candidate re-submits the skill they already hold, alongside a new one.
            SkillNames = new[] { "Kubernetes (provenance)", "Terraform (provenance)" },
        };
        var response = await client.PutAsJsonAsync("/api/candidates/me", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preserved = await ReadCandidateSkillAsync(oid, skillId);
        preserved.Should().NotBeNull();
        preserved!.Source.Should().Be(SkillSource.OpsVerified, "a profile save decides which skills are listed, not who established them");
        preserved.Proficiency.Should().Be(5);
    }

    [Fact]
    public async Task UpdateMe_AddingANewSkill_MarksOnlyThatOneSelfReported()
    {
        var (oid, seededSkillId) = await SeedCandidateWithSkillAsync("Rust (provenance)", SkillSource.OpsVerified, 4);
        var client = CandidateClient(oid);

        var request = new UpdateCandidateProfileRequest
        {
            Availability = Availability.PartTime,
            SkillNames = new[] { "Rust (provenance)", "Elixir (provenance)" },
        };
        (await client.PutAsJsonAsync("/api/candidates/me", request)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var candidate = await db.Candidates
            .Include(c => c.AppUser)
            .Include(c => c.Skills).ThenInclude(cs => cs.Skill)
            .FirstAsync(c => c.AppUser.EntraObjectId == oid);

        candidate.Skills.Should().HaveCount(2);
        candidate.Skills.Single(cs => cs.SkillId == seededSkillId).Source.Should().Be(SkillSource.OpsVerified);
        candidate.Skills.Single(cs => cs.Skill.Name == "Elixir (provenance)").Source.Should().Be(SkillSource.SelfReported);
    }

    /// <summary>Removal still works — the candidate owns their own list, including dropping
    /// something Ops added. The merge must not turn into "additive only".</summary>
    [Fact]
    public async Task UpdateMe_OmittingASkill_RemovesIt()
    {
        var (oid, skillId) = await SeedCandidateWithSkillAsync("COBOL (provenance)", SkillSource.OpsVerified, 3);
        var client = CandidateClient(oid);

        var request = new UpdateCandidateProfileRequest
        {
            Availability = Availability.PartTime,
            SkillNames = new[] { "Go (provenance)" },
        };
        (await client.PutAsJsonAsync("/api/candidates/me", request)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadCandidateSkillAsync(oid, skillId)).Should().BeNull();
    }

    [Fact]
    public async Task UpdateMe_AsSponsor_IsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var request = new UpdateCandidateProfileRequest { Availability = Availability.FullTime };
        var response = await client.PutAsJsonAsync("/api/candidates/me", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
