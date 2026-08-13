using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Skills;
using LaunchPad.Domain.Entities;
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
}
