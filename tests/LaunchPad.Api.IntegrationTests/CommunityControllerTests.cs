using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Community is deliberately [Authorize]-only (any held role) — the mockup shows a
/// mixed-role feed, so there's no ownership boundary to prove here the way there is
/// for Assignments/Projects. These tests prove the post → comment → like round trip
/// works end to end (now via multipart, with an optional image), that cursor pagination
/// is stable and tie-break-safe, that hashtag filtering works, that the image proxy
/// round-trips bytes exactly, and that write-endpoint rate limiting doesn't leak onto
/// reads or reactions.
/// </summary>
public class CommunityControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public CommunityControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static MultipartFormDataContent BuildPostForm(
        string body, CommunityPostType postType, byte[]? imageBytes = null, string? imageContentType = null, string? activeRole = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(body), "body");
        form.Add(new StringContent(postType.ToString()), "postType");
        if (activeRole is not null) form.Add(new StringContent(activeRole), "activeRole");
        if (imageBytes is not null)
        {
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageContentType ?? "image/jpeg");
            form.Add(imageContent, "image", "photo.jpg");
        }
        return form;
    }

    private async Task<int> SeedAppUserAsync(string upn, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var appUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = upn, DisplayName = displayName };
        db.Add(appUser);
        await db.SaveChangesAsync();
        return appUser.AppUserId;
    }

    /// <summary>Mirrors CommunityController.CreatePost's own hashtag-extraction-and-link
    /// flow — a #tag appearing only in Body text (with no CommunityPostHashtag row) would
    /// never actually match a ?hashtag= filter, since that filter is a real join, not a
    /// text scan.</summary>
    private async Task<int> SeedPostAsync(int authorAppUserId, string body, DateTime createdUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var post = new CommunityPost
        {
            AuthorAppUserId = authorAppUserId,
            Body = body,
            PostType = CommunityPostType.Win,
            CreatedUtc = createdUtc,
        };

        foreach (var tag in HashtagExtractor.Extract(body))
        {
            var hashtag = await db.Hashtags.FirstOrDefaultAsync(h => h.Tag == tag) ?? new Hashtag { Tag = tag };
            post.PostHashtags.Add(new CommunityPostHashtag { Post = post, Hashtag = hashtag });
        }

        db.Add(post);
        await db.SaveChangesAsync();
        return post.CommunityPostId;
    }

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

        // Tagged with a unique hashtag so this test can find its own post again via a
        // filtered query, regardless of how many other posts the shared class DB
        // accumulates from other tests (unfiltered pageSize=20 would risk flakiness).
        var tag = $"roundtrip{Guid.NewGuid():N}";
        using var form = BuildPostForm($"Shipped my first feature! #{tag}", CommunityPostType.Win);
        var createResponse = await client.PostAsync("/api/community/posts", form);

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var post = await createResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);
        post!.Body.Should().Contain("Shipped my first feature!");
        post.LikeCount.Should().Be(0);
        post.CommentCount.Should().Be(0);
        post.HasLikedByMe.Should().BeFalse();
        post.HasImage.Should().BeFalse();

        var commentResponse = await client.PostAsJsonAsync(
            $"/api/community/posts/{post.CommunityPostId}/comments", new CreateCommunityCommentRequest { Body = "Nice work!" });
        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactResponse = await client.PostAsync($"/api/community/posts/{post.CommunityPostId}/reactions", content: null);
        reactResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshedResponse = await client.GetAsync($"/api/community/posts?hashtag={tag}");
        refreshedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await refreshedResponse.Content.ReadFromJsonAsync<CommunityFeedPageDto>(TestJsonOptions.Default);
        var refreshed = page!.Items.Single(p => p.CommunityPostId == post.CommunityPostId);
        // Both counters come straight from CommunityPost.LikeCount/CommentCount, never
        // from loading the Reactions/Comments collections — this is really an assertion
        // that the feature still works end to end after that rewrite.
        refreshed.LikeCount.Should().Be(1);
        refreshed.CommentCount.Should().Be(1);
        refreshed.HasLikedByMe.Should().BeTrue();

        var commentsResponse = await client.GetAsync($"/api/community/posts/{post.CommunityPostId}/comments");
        commentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var comments = await commentsResponse.Content.ReadFromJsonAsync<List<CommunityCommentDto>>(TestJsonOptions.Default);
        comments.Should().ContainSingle(c => c.Body == "Nice work!");
    }

    [Fact]
    public async Task ToggleReaction_Twice_RemovesTheLike()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Sponsor);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        using var form = BuildPostForm("Reminder: midpoint reviews next week.", CommunityPostType.Reminder);
        var createResponse = await client.PostAsync("/api/community/posts", form);
        var post = await createResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);

        await client.PostAsync($"/api/community/posts/{post!.CommunityPostId}/reactions", content: null);
        var secondToggle = await client.PostAsync($"/api/community/posts/{post.CommunityPostId}/reactions", content: null);

        secondToggle.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await secondToggle.Content.ReadFromJsonAsync<Dictionary<string, bool>>(TestJsonOptions.Default);
        body!["liked"].Should().BeFalse();
    }

    [Fact]
    public async Task CreatePost_WithImage_RoundTripsBytesThroughTheImageProxy()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var imageBytes = Encoding.UTF8.GetBytes("the exact image content");
        using var form = BuildPostForm("Post with a photo", CommunityPostType.Win, imageBytes, "image/jpeg");

        var createResponse = await client.PostAsync("/api/community/posts", form);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var post = await createResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);
        post!.HasImage.Should().BeTrue();

        var imageResponse = await client.GetAsync($"/api/community/posts/{post.CommunityPostId}/image");
        imageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloadedBytes = await imageResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(imageBytes);
    }

    [Fact]
    public async Task GetPosts_FilteredByHashtag_ReturnsOnlyMatchingPosts_CaseInsensitively()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var tag = $"uniquetag{Guid.NewGuid():N}";
        using var taggedForm = BuildPostForm($"Tagged post #{tag}", CommunityPostType.Win);
        var taggedResponse = await client.PostAsync("/api/community/posts", taggedForm);
        var tagged = await taggedResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);

        using var untaggedForm = BuildPostForm("An unrelated post with no tag", CommunityPostType.Win);
        await client.PostAsync("/api/community/posts", untaggedForm);

        // Deliberately queried with different casing than the tag was posted with.
        var response = await client.GetAsync($"/api/community/posts?hashtag={tag.ToUpperInvariant()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<CommunityFeedPageDto>(TestJsonOptions.Default);
        page!.Items.Should().ContainSingle(i => i.CommunityPostId == tagged!.CommunityPostId);
    }

    [Fact]
    public async Task GetPosts_CursorPagination_ReturnsStablePagesWithNoOverlap_IncludingTieBreak()
    {
        var appUserId = await SeedAppUserAsync("pager@example.com", "Pager");
        var tag = $"pagetest{Guid.NewGuid():N}";
        var tiedTime = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        // Two posts sharing the exact same CreatedUtc — the tie-break case the composite
        // (CreatedUtc, CommunityPostId) index/ordering exists to handle deterministically.
        var postA = await SeedPostAsync(appUserId, $"first #{tag}", tiedTime);
        var postB = await SeedPostAsync(appUserId, $"second #{tag}", tiedTime);
        var postC = await SeedPostAsync(appUserId, $"third #{tag}", tiedTime.AddMinutes(-1));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var page1Response = await client.GetAsync($"/api/community/posts?pageSize=2&hashtag={tag}");
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<CommunityFeedPageDto>(TestJsonOptions.Default);
        page1!.Items.Should().HaveCount(2);
        page1.NextCursor.Should().NotBeNullOrEmpty();
        // Newest-first; ties broken by descending CommunityPostId, so B (inserted after,
        // higher id) sorts before A even though their CreatedUtc is identical.
        page1.Items.Select(i => i.CommunityPostId).Should().Equal(postB, postA);

        var page2Response = await client.GetAsync(
            $"/api/community/posts?pageSize=2&hashtag={tag}&cursor={Uri.EscapeDataString(page1.NextCursor!)}");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Response.Content.ReadFromJsonAsync<CommunityFeedPageDto>(TestJsonOptions.Default);
        page2!.Items.Should().ContainSingle(i => i.CommunityPostId == postC);
        page2.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetPosts_AuthorTeamsLink_MatchesExpectedDeepLinkShape()
    {
        var tag = $"teamslink{Guid.NewGuid():N}";
        var appUserId = await SeedAppUserAsync("author.teamslink@example.com", "Author Name");
        var postId = await SeedPostAsync(appUserId, $"Body text #{tag}", DateTime.UtcNow);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/community/posts?hashtag={tag}");
        var page = await response.Content.ReadFromJsonAsync<CommunityFeedPageDto>(TestJsonOptions.Default);
        var found = page!.Items.Single(i => i.CommunityPostId == postId);

        found.AuthorTeamsLink.Should().Be("https://teams.microsoft.com/l/chat/0/0?users=author.teamslink%40example.com");
    }

    [Fact]
    public async Task CreatePost_ExceedingRateLimit_Returns429_ButReadsAndReactionsAreUnaffected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        int? firstPostId = null;
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 11; i++)
        {
            using var form = BuildPostForm($"Rate limit test post {i}", CommunityPostType.Win);
            lastResponse = await client.PostAsync("/api/community/posts", form);

            if (i == 0 && lastResponse.StatusCode == HttpStatusCode.OK)
            {
                var created = await lastResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);
                firstPostId = created!.CommunityPostId;
            }
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Reads and reactions are excluded from the rate-limit policy — both must still
        // succeed even while this same identity is currently blocked from posting.
        var getResponse = await client.GetAsync("/api/community/posts");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reactResponse = await client.PostAsync($"/api/community/posts/{firstPostId}/reactions", content: null);
        reactResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePost_WithActiveRoleTheCallerHolds_UsesThatRoleAsTheLabel_NotJustTheFirstTokenRole()
    {
        var client = _factory.CreateClient();
        // Candidate listed first on the token — proves the label reflects the requested
        // active role (ActiveRoleContext on the frontend), not simply Roles.FirstOrDefault().
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, $"{Roles.Candidate},{Roles.ProgramOps}");
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        using var form = BuildPostForm("Posting as Ops today", CommunityPostType.Announcement, activeRole: Roles.ProgramOps);
        var response = await client.PostAsync("/api/community/posts", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var post = await response.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);
        post!.AuthorRoleLabel.Should().Be("ProgramOps");
    }

    [Fact]
    public async Task CreatePost_WithActiveRoleTheCallerDoesNotHold_FallsBackToATokenRole_IgnoringTheClaim()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        // Claims a role this identity doesn't actually hold — the server must not trust it.
        using var form = BuildPostForm("Trying to impersonate Ops", CommunityPostType.Win, activeRole: Roles.ProgramOps);
        var response = await client.PostAsync("/api/community/posts", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var post = await response.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);
        post!.AuthorRoleLabel.Should().Be("Candidate");
    }

    [Fact]
    public async Task AddComment_WithActiveRoleTheCallerHolds_UsesThatRoleAsTheLabel()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, $"{Roles.Candidate},{Roles.Sponsor}");
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        using var postForm = BuildPostForm("A post to comment on", CommunityPostType.Win);
        var postResponse = await client.PostAsync("/api/community/posts", postForm);
        var post = await postResponse.Content.ReadFromJsonAsync<CommunityPostDto>(TestJsonOptions.Default);

        var commentResponse = await client.PostAsJsonAsync(
            $"/api/community/posts/{post!.CommunityPostId}/comments",
            new CreateCommunityCommentRequest { Body = "Commenting as Sponsor", ActiveRole = Roles.Sponsor });

        commentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var comment = await commentResponse.Content.ReadFromJsonAsync<CommunityCommentDto>(TestJsonOptions.Default);
        comment!.AuthorRoleLabel.Should().Be("Sponsor");

        // Also confirmed via the read path, not just the write response.
        var commentsResponse = await client.GetAsync($"/api/community/posts/{post.CommunityPostId}/comments");
        var comments = await commentsResponse.Content.ReadFromJsonAsync<List<CommunityCommentDto>>(TestJsonOptions.Default);
        comments!.Single(c => c.CommunityCommentId == comment.CommunityCommentId).AuthorRoleLabel.Should().Be("Sponsor");
    }
}
