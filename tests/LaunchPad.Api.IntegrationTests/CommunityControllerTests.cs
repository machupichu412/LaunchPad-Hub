using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
using LaunchPad.Domain.Enums;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Community is deliberately [Authorize]-only (any held role) — the mockup shows a
/// mixed-role feed, so there's no ownership boundary to prove here the way there is
/// for Assignments/Projects. These tests instead prove the post → comment → like
/// round trip works end to end, and that an unauthenticated caller is rejected by the
/// fail-closed FallbackPolicy.
/// </summary>
public class CommunityControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CommunityControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetPosts_Unauthenticated_IsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/community/posts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePost_ThenCommentAndReact_RoundTripsCorrectly()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var createResponse = await client.PostAsJsonAsync("/api/community/posts", new CreateCommunityPostRequest { Body = "Shipped my first feature!", PostType = CommunityPostType.Win });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var post = await createResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);
        post!.Body.Should().Be("Shipped my first feature!");
        post.LikeCount.Should().Be(0);
        post.HasLikedByMe.Should().BeFalse();

        var commentResponse = await client.PostAsJsonAsync($"/api/community/posts/{post.CommunityPostId}/comments", new CreateCommunityCommentRequest { Body = "Nice work!" });
        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactResponse = await client.PostAsync($"/api/community/posts/{post.CommunityPostId}/reactions", content: null);
        reactResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var postsResponse = await client.GetAsync("/api/community/posts");
        postsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var posts = await postsResponse.Content.ReadFromJsonAsync<List<CommunityPostDto>>(TestJsonOptions.Default);
        var refreshed = posts!.Single(p => p.CommunityPostId == post.CommunityPostId);
        refreshed.LikeCount.Should().Be(1);
        refreshed.HasLikedByMe.Should().BeTrue();
        refreshed.Comments.Should().ContainSingle(c => c.Body == "Nice work!");
    }

    [Fact]
    public async Task ToggleReaction_Twice_RemovesTheLike()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var createResponse = await client.PostAsJsonAsync("/api/community/posts", new CreateCommunityPostRequest { Body = "Reminder: midpoint reviews next week.", PostType = CommunityPostType.Reminder });
        var post = await createResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);

        await client.PostAsync($"/api/community/posts/{post!.CommunityPostId}/reactions", content: null);
        var secondToggle = await client.PostAsync($"/api/community/posts/{post.CommunityPostId}/reactions", content: null);

        secondToggle.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await secondToggle.Content.ReadFromJsonAsync<Dictionary<string, bool>>(TestJsonOptions.Default);
        body!["liked"].Should().BeFalse();
    }
}
