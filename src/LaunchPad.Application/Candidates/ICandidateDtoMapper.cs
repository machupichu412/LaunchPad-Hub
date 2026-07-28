using System.Security.Claims;
using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Candidates;

/// <summary>
/// The single most important control in the app: redaction of hidden numeric
/// ratings happens here, server-side, and nowhere else. Implementations must
/// leave AverageScore/HasPerformanceRisk/HasEngagementRisk null unless the caller
/// is Executive or ProgramOps — additive population only, never subtractive.
/// </summary>
public interface ICandidateDtoMapper
{
    CandidateDto ToDto(Candidate candidate, CandidateRisk? risk, ClaimsPrincipal user);
}
