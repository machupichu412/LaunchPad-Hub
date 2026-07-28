using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.ViewTalentPipeline)]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateRepository _candidates;
    private readonly ICandidateDtoMapper _mapper;

    public CandidatesController(ICandidateRepository candidates, ICandidateDtoMapper mapper)
    {
        _candidates = candidates;
        _mapper = mapper;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CandidateDto>> Get(int id, CancellationToken ct)
    {
        var candidate = await _candidates.GetWithSkillsAsync(id, ct);
        if (candidate is null) return NotFound();

        // Redaction happens inside the mapper — never filter scores here or in the client.
        var risk = await _candidates.GetRiskAsync(id, ct);
        return Ok(_mapper.ToDto(candidate, risk, User));
    }

    [HttpGet("cohort/{cohortId:int}")]
    public async Task<ActionResult<IReadOnlyList<CandidateDto>>> GetByCohort(int cohortId, CancellationToken ct)
    {
        var candidates = await _candidates.GetByCohortAsync(cohortId, ct);
        var dtos = new List<CandidateDto>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var risk = await _candidates.GetRiskAsync(candidate.CandidateId, ct);
            dtos.Add(_mapper.ToDto(candidate, risk, User));
        }

        return Ok(dtos);
    }
}
