using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Every notification is scoped to the caller's own AppUserId, resolved server-side —
/// these tests prove that boundary (one user can never read or mark-read another's).
/// </summary>
public class NotificationsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public NotificationsControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private async Task<(Guid Oid, int NotificationId)> SeedNotificationAsync(bool isRead = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var oid = Guid.NewGuid();
        var appUser = new AppUser { EntraObjectId = oid, Upn = $"{Guid.NewGuid()}@example.com", DisplayName = "Notif Test User" };
        var notification = new Notification
        {
            RecipientAppUser = appUser,
            Subject = "Test subject",
            Body = "Test body",
            IsRead = isRead,
        };

        db.AddRange(appUser, notification);
        await db.SaveChangesAsync();

        return (oid, notification.NotificationId);
    }

    [Fact]
    public async Task Get_ReturnsOnlyTheCallersOwnNotifications()
    {
        var (oid, notificationId) = await SeedNotificationAsync();
        await SeedNotificationAsync(); // someone else's — must not appear

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>(TestJsonOptions.Default);
        notifications.Should().ContainSingle(n => n.NotificationId == notificationId);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyUnreadForTheCaller()
    {
        var (oid, _) = await SeedNotificationAsync(isRead: false);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.GetAsync("/api/notifications/unread-count");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await response.Content.ReadFromJsonAsync<int>(TestJsonOptions.Default);
        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkRead_AsTheRecipient_Succeeds()
    {
        var (oid, notificationId) = await SeedNotificationAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.PostAsync($"/api/notifications/{notificationId}/read", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        (await db.Notifications.FindAsync(notificationId))!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkRead_AsSomeoneElse_IsForbidden()
    {
        var (_, notificationId) = await SeedNotificationAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsync($"/api/notifications/{notificationId}/read", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkAllRead_ClearsEveryUnreadNotificationForTheCaller_ButNotOthers()
    {
        var (oid, firstId) = await SeedNotificationAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
            var appUser = await db.AppUsers.FindAsync((await db.Notifications.FindAsync(firstId))!.RecipientAppUserId);
            db.Notifications.Add(new Notification { RecipientAppUser = appUser!, Subject = "Second", Body = "Body", IsRead = false });
            await db.SaveChangesAsync();
        }

        var (_, otherPersonsNotificationId) = await SeedNotificationAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Candidate);
        client.DefaultRequestHeaders.Add(TestAuthHandler.OidHeader, oid.ToString());

        var response = await client.PostAsync("/api/notifications/read-all", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        (await verifyDb.Notifications.FindAsync(firstId))!.IsRead.Should().BeTrue();
        (await verifyDb.Notifications.FindAsync(otherPersonsNotificationId))!.IsRead.Should().BeFalse();
    }
}
