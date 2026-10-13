using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.ProjectSupport;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

public class ProjectSupportControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory factory;
    public ProjectSupportControllerTests(CustomWebApplicationFactory factory) => this.factory = factory;
    private HttpClient Client(string role, Guid oid)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());
        return client;
    }
    private async Task<(Project Project, Project Other, AppUser Expert, AppUser Candidate, Deliverable Deliverable)> Seed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var expert = new AppUser { EntraObjectId = Guid.NewGuid(), DisplayName = "Expert", IsSubjectMatterExpert = true };
        var candidateUser = new AppUser { EntraObjectId = Guid.NewGuid(), DisplayName = "Candidate" };
        var cohort = new Cohort { Name = "Support cohort", Program = new LaunchPad.Domain.Entities.Program { Name = "Program" } };
        var sponsor = new Sponsor { AppUser = new AppUser { EntraObjectId = Guid.NewGuid() } };
        var project = new Project { Name = "Assigned project", Cohort = cohort, Sponsor = sponsor };
        var other = new Project { Name = "Unassigned project", Cohort = cohort, Sponsor = sponsor };
        var candidate = new Candidate { AppUser = candidateUser, Cohort = cohort };
        var assignment = new Assignment { Project = project, Candidate = candidate, Status = AssignmentStatus.Active, MatchScore = 99 };
        var deliverable = new Deliverable { Assignment = assignment, Title = "Prototype", FileName = "prototype.txt" };
        db.AddRange(project, other, expert, deliverable);
        db.Add(new ProjectExpert { Project = project, AppUser = expert });
        await db.SaveChangesAsync();
        return (project, other, expert, candidateUser, deliverable);
    }

    [Fact]
    public async Task Expert_access_is_project_scoped_and_does_not_grant_pipeline_or_mutations()
    {
        var s = await Seed();
        var client = Client(Roles.SubjectMatterExpert, s.Expert.EntraObjectId);
        var projects = await client.GetFromJsonAsync<SupportProjectDto[]>("/api/project-support/projects");
        projects.Should().ContainSingle().Which.ProjectId.Should().Be(s.Project.ProjectId);
        (await client.GetAsync($"/api/project-support/projects/{s.Other.ProjectId}/questions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/project-support/projects/{s.Other.ProjectId}/deliverables")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/projects/{s.Other.ProjectId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/candidates/cohort/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsync($"/api/project-support/projects/{s.Other.ProjectId}/experts/{s.Expert.AppUserId}", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var json = await client.GetStringAsync($"/api/project-support/projects/{s.Project.ProjectId}/deliverables");
        json.Should().Contain("Prototype").And.NotContain("matchScore").And.NotContain("averageScore");
    }

    [Fact]
    public async Task Candidate_question_expert_attachment_and_deliverable_review_round_trip()
    {
        var s = await Seed();
        var candidate = Client(Roles.Candidate, s.Candidate.EntraObjectId);
        var expert = Client(Roles.SubjectMatterExpert, s.Expert.EntraObjectId);
        var root = $"/api/project-support/projects/{s.Project.ProjectId}";
        (await candidate.PostAsJsonAsync(root + "/questions", new CreateQuestionRequest("API design", "How should this work?"))).EnsureSuccessStatusCode();
        var question = (await expert.GetFromJsonAsync<QuestionDto[]>(root + "/questions"))!.Single();
        var bytes = Encoding.UTF8.GetBytes("Technical guidance");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Try this approach."), "body");
        form.Add(new ByteArrayContent(bytes), "file", "guidance.txt");
        (await expert.PostAsync(root + $"/questions/{question.ProjectQuestionId}/answers", form)).EnsureSuccessStatusCode();
        var answer = (await candidate.GetFromJsonAsync<QuestionDto[]>(root + "/questions"))!.Single().Answers.Single();
        var filePath = root + $"/questions/{question.ProjectQuestionId}/answers/{answer.ProjectAnswerId}/file";
        (await candidate.GetByteArrayAsync(filePath)).Should().Equal(bytes);
        var outsider = Client(Roles.SubjectMatterExpert, Guid.NewGuid());
        (await outsider.GetAsync(filePath)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await expert.GetAsync($"/api/project-support/projects/{s.Other.ProjectId}/questions/{question.ProjectQuestionId}/answers/{answer.ProjectAnswerId}/file")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await expert.PostAsJsonAsync(root + "/questions", new CreateQuestionRequest("Review", "Looks good; add error handling.", s.Deliverable.DeliverableId))).EnsureSuccessStatusCode();
        (await candidate.GetFromJsonAsync<QuestionDto[]>(root + "/questions"))!.Should().Contain(q => q.DeliverableId == s.Deliverable.DeliverableId);
        (await candidate.PostAsJsonAsync(root + "/questions", new CreateQuestionRequest("Fake review", "Not an SME", s.Deliverable.DeliverableId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await candidate.PostAsJsonAsync($"/api/project-support/projects/{s.Other.ProjectId}/questions", new CreateQuestionRequest("No", "No"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ops_can_assign_multiple_projects_and_removal_revokes_access()
    {
        var s = await Seed();
        var ops = Client(Roles.ProgramOps, Guid.NewGuid());
        var expert = Client(Roles.SubjectMatterExpert, s.Expert.EntraObjectId);
        var path = $"/api/project-support/projects/{s.Other.ProjectId}/experts/{s.Expert.AppUserId}";
        (await ops.PutAsync(path, null)).EnsureSuccessStatusCode();
        (await ops.PutAsync(path, null)).EnsureSuccessStatusCode();
        (await expert.GetFromJsonAsync<SupportProjectDto[]>("/api/project-support/projects"))!.Should().HaveCount(2);
        (await ops.DeleteAsync(path)).EnsureSuccessStatusCode();
        (await expert.GetAsync($"/api/project-support/projects/{s.Other.ProjectId}/questions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ops.DeleteAsync($"/api/project-support/projects/{s.Project.ProjectId}/experts/{s.Expert.AppUserId}")).EnsureSuccessStatusCode();
        (await expert.GetFromJsonAsync<SupportProjectDto[]>("/api/project-support/projects"))!.Should().BeEmpty();
        using var post = new MultipartFormDataContent();
        post.Add(new StringContent("A technical tip"), "body");
        post.Add(new StringContent("Win"), "postType");
        (await expert.PostAsync("/api/community/posts", post)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Attachments_reject_invalid_content_and_foreign_deliverables()
    {
        var s = await Seed();
        var expert = Client(Roles.SubjectMatterExpert, s.Expert.EntraObjectId);
        var root = $"/api/project-support/projects/{s.Project.ProjectId}";
        (await expert.PostAsJsonAsync(root + "/questions", new CreateQuestionRequest("Review", "Feedback", s.Deliverable.DeliverableId))).EnsureSuccessStatusCode();
        var q = (await expert.GetFromJsonAsync<QuestionDto[]>(root + "/questions"))!.Single();
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("not a png")), "file", "fake.png");
        (await expert.PostAsync(root + $"/questions/{q.ProjectQuestionId}/answers", invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await expert.PostAsJsonAsync(root + "/questions", new CreateQuestionRequest("Review", "Feedback", int.MaxValue))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(AssignmentStatus.Proposed)]
    [InlineData(AssignmentStatus.SponsorApproved)]
    [InlineData(AssignmentStatus.Withdrawn)]
    public async Task Uncommitted_or_withdrawn_candidate_cannot_read_project_questions(AssignmentStatus status)
    {
        var s = await Seed();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            db.Assignments.Single(a => a.ProjectId == s.Project.ProjectId).Status = status;
            await db.SaveChangesAsync();
        }
        var candidate = Client(Roles.Candidate, s.Candidate.EntraObjectId);
        (await candidate.GetAsync($"/api/project-support/projects/{s.Project.ProjectId}/questions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// Run the same access and discussion workflows on a relational provider as well,
// so query translation and foreign keys are exercised in addition to InMemory.
[Collection("Sqlite")]
public class ProjectSupportRelationalTests : ProjectSupportControllerTests
{
    public ProjectSupportRelationalTests(LaunchPad.Api.IntegrationTests.Sqlite.SqliteFixture fixture) : base(fixture.Factory) { }
}
