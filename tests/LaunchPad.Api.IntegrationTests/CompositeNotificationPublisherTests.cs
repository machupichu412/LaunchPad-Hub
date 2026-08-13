using FluentAssertions;
using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Notifications;
using LaunchPad.Infrastructure.Persistence;
using LaunchPad.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Exercises CompositeNotificationPublisher directly against an isolated in-memory
/// DbContext rather than through CustomWebApplicationFactory — that factory always
/// swaps in FakeNotificationPublisher (needed so the existing Submit/Approve/Reject
/// tests can assert on .Sent), which would bypass exactly the write-through logic
/// this class exists to prove. ServiceBusNotificationPublisher itself never throws
/// when ServiceBus:Namespace isn't configured (see its own early-return branch), so
/// it's safe to use unmodified here — no fake needed for it either.
/// </summary>
public class CompositeNotificationPublisherTests
{
    private static LaunchPadDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<LaunchPadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CompositeNotificationPublisher CreateSut(LaunchPadDbContext db) =>
        new(
            new NotificationRepository(db),
            new AppUserRepository(db),
            new ServiceBusNotificationPublisher(new ConfigurationBuilder().Build(), NullLogger<ServiceBusNotificationPublisher>.Instance));

    [Fact]
    public async Task PublishAsync_WhenToUpnMatchesAnAppUser_WritesAnUnreadNotificationRow()
    {
        var db = CreateDb();
        var appUser = new AppUser { EntraObjectId = Guid.NewGuid(), Upn = "recipient@example.com", DisplayName = "Recipient" };
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        await CreateSut(db).PublishAsync(new NotificationMessage("recipient@example.com", "Subject line", "Body text"));

        var stored = await db.Notifications.SingleAsync();
        stored.RecipientAppUserId.Should().Be(appUser.AppUserId);
        stored.Subject.Should().Be("Subject line");
        stored.Body.Should().Be("Body text");
        stored.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_WhenToUpnDoesNotMatchAnyAppUser_SkipsTheInAppRowWithoutThrowing()
    {
        var db = CreateDb();

        var act = () => CreateSut(db).PublishAsync(new NotificationMessage("unmapped-config-only@example.com", "Subject", "Body"));

        await act.Should().NotThrowAsync();
        (await db.Notifications.AnyAsync()).Should().BeFalse();
    }
}
