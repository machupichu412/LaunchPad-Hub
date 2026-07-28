using System.Security.Claims;
using FluentAssertions;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;
using Xunit;

namespace LaunchPad.Application.Tests.Candidates;

/// <summary>
/// The redaction pattern in CandidateDtoMapper is the single most important security
/// control in the app (see CLAUDE.md). These tests are the unit-level analogue of the
/// per-role API integration test suite required before merge.
/// </summary>
public class CandidateDtoMapperTests
{
    private readonly CandidateDtoMapper _sut = new();

    private static Candidate MakeCandidate() => new()
    {
        CandidateId = 1,
        AppUser = new AppUser { DisplayName = "Jordan Rivera" },
        Skills = new List<CandidateSkill>(),
        Status = CandidateStatus.InProgress
    };

    private static CandidateRisk MakeRisk() => new()
    {
        CandidateId = 1,
        AvgScore = 2.1m,
        HasPerformanceRisk = true,
        HasEngagementRisk = false
    };

    private static ClaimsPrincipal MakeUser(params string[] roles)
    {
        var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(identity);
    }

    [Theory]
    [InlineData(Roles.Sponsor)]
    [InlineData(Roles.Candidate)]
    [InlineData(Roles.HiringManager)]
    public void ToDto_NeverExposesScoresOrRiskFlags_ForUnauthorizedRoles(string role)
    {
        var dto = _sut.ToDto(MakeCandidate(), MakeRisk(), MakeUser(role));

        dto.AverageScore.Should().BeNull();
        dto.HasPerformanceRisk.Should().BeNull();
        dto.HasEngagementRisk.Should().BeNull();
    }

    [Theory]
    [InlineData(Roles.Executive)]
    [InlineData(Roles.ProgramOps)]
    public void ToDto_ExposesScoresAndRiskFlags_ForAuthorizedRoles(string role)
    {
        var dto = _sut.ToDto(MakeCandidate(), MakeRisk(), MakeUser(role));

        dto.AverageScore.Should().Be(2.1m);
        dto.HasPerformanceRisk.Should().BeTrue();
        dto.HasEngagementRisk.Should().BeFalse();
    }

    [Fact]
    public void ToDto_NeverExposesScores_WhenRiskIsNullEvenForAuthorizedRoles()
    {
        var dto = _sut.ToDto(MakeCandidate(), risk: null, MakeUser(Roles.Executive));

        dto.AverageScore.Should().BeNull();
    }
}
