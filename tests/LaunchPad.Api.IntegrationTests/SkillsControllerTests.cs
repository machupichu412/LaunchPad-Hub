using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Skills;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

public class SkillsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public SkillsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<int> SeedCategoryAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var category = new SkillCategory { Name = name };
        db.Add(category);
        await db.SaveChangesAsync();
        return category.SkillCategoryId;
    }

    private async Task<int> SeedUnusedSkillAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var skill = new Skill { Name = name, SkillCategory = new SkillCategory { Name = $"{name} Category" } };
        db.Add(skill);
        await db.SaveChangesAsync();
        return skill.SkillId;
    }

    private async Task<int> SeedSkillInUseByCandidateAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var skill = new Skill { Name = name, SkillCategory = new SkillCategory { Name = $"{name} Category" } };
        var program = new Domain.Entities.Program { Name = "Skill Delete Program" };
        var cohort = new Cohort { Program = program, Name = "Skill Delete Cohort", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1), Status = CohortStatus.Active };
        var candidate = new Candidate
        {
            Cohort = cohort,
            AppUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = $"{name.ToLowerInvariant()}-owner@example.com", DisplayName = "Skill Owner" },
            Availability = Availability.PartTime,
            Status = CandidateStatus.InProgress,
            Skills = new List<CandidateSkill> { new() { Skill = skill, Proficiency = 3, Source = SkillSource.SelfReported } },
        };

        db.AddRange(program, cohort, candidate, skill);
        await db.SaveChangesAsync();
        return skill.SkillId;
    }

    [Fact]
    public async Task GetSkills_AsCandidate_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.GetAsync("/api/skills");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCategories_AsSponsor_Succeeds()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.GetAsync("/api/skills/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_WithNoAuth_IsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/skills");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_AsCandidate_CreatesSkillUnderCategory()
    {
        var categoryId = await SeedCategoryAsync("Data");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var response = await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest { Name = "Snowflake", SkillCategoryId = categoryId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<SkillDto>(TestJsonOptions.Default);
        dto!.Name.Should().Be("Snowflake");
        dto.SkillCategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task Create_WithNameThatAlreadyExists_ReturnsExistingSkill_NotADuplicate()
    {
        var categoryId = await SeedCategoryAsync("Design");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);

        var first = await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest { Name = "Figma", SkillCategoryId = categoryId });
        var firstDto = await first.Content.ReadFromJsonAsync<SkillDto>(TestJsonOptions.Default);

        var second = await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest { Name = "figma", SkillCategoryId = categoryId });
        var secondDto = await second.Content.ReadFromJsonAsync<SkillDto>(TestJsonOptions.Default);

        secondDto!.SkillId.Should().Be(firstDto!.SkillId);
    }

    [Fact]
    public async Task Create_AsSponsor_IsForbidden()
    {
        var categoryId = await SeedCategoryAsync("Cloud");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest { Name = "Kubernetes", SkillCategoryId = categoryId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsProgramOps_Succeeds()
    {
        var categoryId = await SeedCategoryAsync("Ops-Added");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.PostAsJsonAsync("/api/skills", new CreateSkillRequest { Name = "Terraform", SkillCategoryId = categoryId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_AsProgramOps_RemovesUnusedSkill()
    {
        var skillId = await SeedUnusedSkillAsync("Deletable");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.DeleteAsync($"/api/skills/{skillId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await client.GetAsync("/api/skills");
        var skills = await listResponse.Content.ReadFromJsonAsync<List<SkillDto>>(TestJsonOptions.Default);
        skills!.Should().NotContain(s => s.SkillId == skillId);
    }

    [Fact]
    public async Task Delete_AsSponsor_IsForbidden()
    {
        var skillId = await SeedUnusedSkillAsync("NotYours");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);

        var response = await client.DeleteAsync($"/api/skills/{skillId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_SkillInUseByCandidate_ReturnsConflict()
    {
        var skillId = await SeedSkillInUseByCandidateAsync("StillNeeded");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.DeleteAsync($"/api/skills/{skillId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var listResponse = await client.GetAsync("/api/skills");
        var skills = await listResponse.Content.ReadFromJsonAsync<List<SkillDto>>(TestJsonOptions.Default);
        skills!.Should().Contain(s => s.SkillId == skillId);
    }

    [Fact]
    public async Task Delete_UnknownSkillId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.ProgramOps);

        var response = await client.DeleteAsync("/api/skills/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
