using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using LaunchPad.Application.Common;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// AppUser rows are JIT-provisioned by AppUserProvisioningMiddleware on first
/// authenticated request (see Program.cs) — no manual seeding needed here, unlike
/// tests that need a role-specific row (Sponsor/Candidate). Each test uses its own
/// X-Test-Oid so the shared class-fixture DB never lets one test's avatar leak into
/// another's assertions.
/// </summary>
public class MeControllerAvatarTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public MeControllerAvatarTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(Guid oid)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());
        return client;
    }

    private static ByteArrayContent ImageContent(byte[] bytes, string contentType = "image/jpeg")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    [Fact]
    public async Task UploadThenGet_RoundTripsTheSameBytesAndContentType()
    {
        var client = CreateClient(Guid.NewGuid());
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        var uploadResponse = await client.PostAsync("/api/me/avatar", ImageContent(bytes, "image/png"));
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/me/avatar");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await getResponse.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task Get_WithNoAvatarUploaded_ReturnsNotFound()
    {
        var client = CreateClient(Guid.NewGuid());

        var response = await client.GetAsync("/api/me/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_WithDisallowedContentType_ReturnsBadRequest()
    {
        var client = CreateClient(Guid.NewGuid());

        var response = await client.PostAsync("/api/me/avatar", ImageContent(new byte[] { 1, 2, 3 }, "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OverTheSizeCap_ReturnsBadRequest()
    {
        var client = CreateClient(Guid.NewGuid());
        var tooLarge = new byte[(2 * 1024 * 1024) + 1];

        var response = await client.PostAsync("/api/me/avatar", ImageContent(tooLarge));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_ClearsTheAvatar_SoASubsequentGetIsNotFound()
    {
        var client = CreateClient(Guid.NewGuid());
        await client.PostAsync("/api/me/avatar", ImageContent(new byte[] { 9, 9, 9 }));

        var deleteResponse = await client.DeleteAsync("/api/me/avatar");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/me/avatar");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TwoDifferentUsers_EachSeeOnlyTheirOwnAvatar()
    {
        var clientA = CreateClient(Guid.NewGuid());
        var clientB = CreateClient(Guid.NewGuid());

        await clientA.PostAsync("/api/me/avatar", ImageContent(new byte[] { 1, 1, 1 }));
        await clientB.PostAsync("/api/me/avatar", ImageContent(new byte[] { 2, 2, 2 }));

        var aBytes = await (await clientA.GetAsync("/api/me/avatar")).Content.ReadAsByteArrayAsync();
        var bBytes = await (await clientB.GetAsync("/api/me/avatar")).Content.ReadAsByteArrayAsync();

        aBytes.Should().BeEquivalentTo(new byte[] { 1, 1, 1 });
        bBytes.Should().BeEquivalentTo(new byte[] { 2, 2, 2 });
    }

    [Fact]
    public async Task Upload_Twice_ReplacesThePreviousImage()
    {
        var client = CreateClient(Guid.NewGuid());

        await client.PostAsync("/api/me/avatar", ImageContent(new byte[] { 1, 1, 1 }));
        await client.PostAsync("/api/me/avatar", ImageContent(new byte[] { 2, 2, 2, 2 }));

        var getResponse = await client.GetAsync("/api/me/avatar");
        (await getResponse.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(new byte[] { 2, 2, 2, 2 });
    }
}
