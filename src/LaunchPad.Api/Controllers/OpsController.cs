using LaunchPad.Application.Common;
using LaunchPad.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

// Cross-cohort admin views (Dashboard, Risks) — gated by ViewHiddenScores (Ops+Exec)
// since this is exactly the same class of redacted, score-adjacent data that policy
// already exists for; Executive's own dashboard can reuse these reads later.
[ApiController]
[Route("api/ops")]
[Authorize(Policy = Policies.ViewHiddenScores)]
public class OpsController : ControllerBase
{
    private readonly IOpsDashboardRepository _dashboard;

    public OpsController(IOpsDashboardRepository dashboard) => _dashboard = dashboard;

    [HttpGet("dashboard")]
    public async Task<ActionResult<OpsDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await _dashboard.GetDashboardAsync(ct));

    [HttpGet("risks")]
    public async Task<ActionResult<IReadOnlyList<RiskCandidateDto>>> GetRisks(CancellationToken ct) =>
        Ok(await _dashboard.GetAtRiskCandidatesAsync(take: null, ct: ct));
}
