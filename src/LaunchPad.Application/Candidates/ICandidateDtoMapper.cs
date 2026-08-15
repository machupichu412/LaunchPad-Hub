using System.Security.Claims;
using LaunchPad.Application.Risk;
using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Candidates;

/// <summary>
/// The single most important control in the app: redaction of hidden numeric
/// ratings happens here, server-side, and nowhere else. Implementations must
/// leave AverageScore/HasPerformanceRisk/HasEngagementRisk/SuggestedHireOutcome null
/// unless the caller is Executive or ProgramOps — additive population only, never
/// subtractive. suggestedHireOutcome is precomputed by the caller via HireOutcomeRule
/// (a pure function) — this mapper only decides whether the caller's role gets to see it.
/// </summary>
public interface ICandidateDtoMapper
{
    CandidateDto ToDto(Candidate candidate, CandidateRisk? risk, SuggestedHireOutcome? suggestedHireOutcome, ClaimsPrincipal user);
}
