using System.Security.Claims;
using LaunchPad.Application.Common;
using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Candidates;

public sealed class CandidateDtoMapper : ICandidateDtoMapper
{
    public CandidateDto ToDto(Candidate candidate, CandidateRisk? risk, ClaimsPrincipal user)
    {
        var dto = new CandidateDto
        {
            CandidateId = candidate.CandidateId,
            DisplayName = candidate.AppUser.DisplayName,
            Email = candidate.AppUser.Upn,
            Location = candidate.Location,
            Availability = candidate.Availability,
            GraduationDate = candidate.GraduationDate,
            LinkedInUrl = candidate.LinkedInUrl,
            PortfolioUrl = candidate.PortfolioUrl,
            Bio = candidate.Bio,
            School = candidate.School,
            Degree = candidate.Degree,
            Gpa = candidate.Gpa,
            Skills = candidate.Skills.Select(s => s.Skill.Name).ToArray(),
            Outcome = candidate.Status.ToOutcomeLabel()
        };

        if (user.IsInRole(Roles.Executive) || user.IsInRole(Roles.ProgramOps))
        {
            dto.AverageScore = risk?.AvgScore;
            dto.HasPerformanceRisk = risk?.HasPerformanceRisk;
            dto.HasEngagementRisk = risk?.HasEngagementRisk;
        }

        return dto;
    }
}
