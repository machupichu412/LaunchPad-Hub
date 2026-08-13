using FluentAssertions;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// AuditLog's read path (GetEntityIdsWithLatestActionAsync) is the dedup mechanism
/// NightlyRiskRecalculationFunction relies on to avoid re-flagging/re-notifying every
/// night a risk flag stays true — these tests cover that logic directly since no HTTP
/// endpoint exercises it (the function itself has no test harness in this repo; see
/// the "sponsor auto-flag" slice's scoping notes).
/// </summary>
public class AuditLogTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public AuditLogTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RecordAsync_WithSystemActor_UsesZeroAsActorAppUserId()
    {
        using var scope = _factory.Services.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();

        var entityId = Guid.NewGuid().ToString();
        await auditLog.RecordAsync(Guid.Empty, "Candidate", entityId, "AutoFlagged");

        var recorded = await db.AuditEvents.SingleAsync(e => e.EntityId == entityId);
        recorded.ActorAppUserId.Should().Be(0);
        recorded.Action.Should().Be("AutoFlagged");
    }

    [Fact]
    public async Task RecordAsync_ResolvesActorAppUserId_FromEntraObjectId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaunchPadDbContext>();
        var actorOid = Guid.NewGuid();
        var appUser = new AppUser { EntraObjectId = actorOid, Upn = "auditor@example.com", DisplayName = "Auditor" };
        db.AppUsers.Add(appUser);
        await db.SaveChangesAsync();

        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var entityId = Guid.NewGuid().ToString();
        await auditLog.RecordAsync(actorOid, "Project", entityId, "Approve");

        var recorded = await db.AuditEvents.SingleAsync(e => e.EntityId == entityId);
        recorded.ActorAppUserId.Should().Be(appUser.AppUserId);
    }

    [Fact]
    public async Task GetEntityIdsWithLatestActionAsync_ReturnsOnlyEntitiesWhoseMostRecentActionMatches()
    {
        using var scope = _factory.Services.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        // candidateA: flagged, still flagged (no later event) -> should be returned.
        // candidateB: flagged, then cleared -> should NOT be returned.
        // candidateC: never touched -> should NOT be returned.
        var candidateA = new Random().Next(100_000, 200_000);
        var candidateB = candidateA + 1;

        await auditLog.RecordAsync(Guid.Empty, "Candidate", candidateA.ToString(), "AutoFlagged");
        await auditLog.RecordAsync(Guid.Empty, "Candidate", candidateB.ToString(), "AutoFlagged");
        await auditLog.RecordAsync(Guid.Empty, "Candidate", candidateB.ToString(), "AutoFlagCleared");

        var flagged = await auditLog.GetEntityIdsWithLatestActionAsync("Candidate", "AutoFlagged");

        flagged.Should().Contain(candidateA);
        flagged.Should().NotContain(candidateB);
    }
}
