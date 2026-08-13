using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Common;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Search;
using LaunchPad.Application.Sponsors;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Global header search. Kept intentionally simple: a case-insensitive Contains over
/// names/skills, same as TalentPipeline/ProjectMarketplace's existing client-side
/// search inputs — just server-side and role-scoped since it spans data the current
/// page hasn't necessarily loaded. Reuses the same repository methods and cohort
/// simplification (DemoCohortId) already used by TalentPipeline/OpsProjects/
/// ApprovalQueue pending real cohort selection — not a new scoping decision.
/// </summary>
[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private const int MaxResultsPerCategory = 8;
    private const int DemoCohortId = 1;

    private readonly IProjectRepository _projects;
    private readonly ICandidateRepository _candidates;
    private readonly ISponsorRepository _sponsors;
    private readonly IAssignmentRepository _assignments;
    private readonly ICurrentUser _currentUser;

    public SearchController(
        IProjectRepository projects,
        ICandidateRepository candidates,
        ISponsorRepository sponsors,
        IAssignmentRepository assignments,
        ICurrentUser currentUser)
    {
        _projects = projects;
        _candidates = candidates;
        _sponsors = sponsors;
        _assignments = assignments;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Get([FromQuery] string? q, CancellationToken ct)
    {
        var term = (q ?? string.Empty).Trim();
        if (term.Length < 2) return Ok(Array.Empty<SearchResultDto>());

        var results = new List<SearchResultDto>();

        if (_currentUser.IsInRole(Roles.Candidate))
        {
            var candidate = await _candidates.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
            if (candidate is not null)
            {
                var openProjects = await _projects.GetOpenByCohortAsync(candidate.CohortId, ct);
                results.AddRange(MatchProjects(openProjects, term, p => $"/marketplace/{p.ProjectId}"));
            }
        }
        else if (_currentUser.IsInRole(Roles.Sponsor))
        {
            var sponsor = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
            if (sponsor is not null)
            {
                var ownProjects = await _projects.GetBySponsorAsync(sponsor.SponsorId, ct);
                results.AddRange(MatchProjects(ownProjects, term, p => $"/projects/{p.ProjectId}/matches"));

                var ownAssignments = await _assignments.GetBySponsorAsync(sponsor.SponsorId, ct);
                results.AddRange(MatchAssignedCandidates(ownAssignments, term));
            }
        }
        else if (_currentUser.IsInRole(Roles.ProgramOps))
        {
            var cohortProjects = await _projects.GetByCohortAsync(DemoCohortId, ct);
            results.AddRange(MatchProjects(cohortProjects, term, p => $"/ops/projects/{p.ProjectId}"));

            var cohortCandidates = await _candidates.GetByCohortAsync(DemoCohortId, ct);
            results.AddRange(MatchCandidates(cohortCandidates, term, "/pipeline"));
        }
        else if (_currentUser.IsInRole(Roles.Executive) || _currentUser.IsInRole(Roles.HiringManager))
        {
            // No project-browsing route exists for these roles today — only the
            // ProgramOps-only /ops/projects/{id} and the Sponsor's own /projects.
            var cohortCandidates = await _candidates.GetByCohortAsync(DemoCohortId, ct);
            results.AddRange(MatchCandidates(cohortCandidates, term, "/pipeline"));
        }

        return Ok(results);
    }

    private static IEnumerable<SearchResultDto> MatchProjects(IEnumerable<Project> projects, string term, Func<Project, string> urlFor) =>
        projects
            .Where(p => Matches(p.Name, term) || p.Skills.Any(s => Matches(s.Skill.Name, term)))
            .Take(MaxResultsPerCategory)
            .Select(p => new SearchResultDto
            {
                Type = "Project",
                Id = p.ProjectId,
                Title = p.Name,
                Subtitle = p.Sponsor.AppUser.DisplayName,
                Url = urlFor(p),
            });

    private static IEnumerable<SearchResultDto> MatchCandidates(IEnumerable<Candidate> candidates, string term, string url) =>
        candidates
            .Where(c => Matches(c.AppUser.DisplayName, term) || Matches(c.School, term))
            .Take(MaxResultsPerCategory)
            .Select(c => new SearchResultDto
            {
                Type = "Candidate",
                Id = c.CandidateId,
                Title = c.AppUser.DisplayName,
                Subtitle = c.School ?? string.Empty,
                Url = url,
            });

    private static IEnumerable<SearchResultDto> MatchAssignedCandidates(IEnumerable<Assignment> assignments, string term) =>
        assignments
            .Where(a => Matches(a.Candidate.AppUser.DisplayName, term))
            .GroupBy(a => a.CandidateId)
            .Take(MaxResultsPerCategory)
            .Select(g => g.First())
            .Select(a => new SearchResultDto
            {
                Type = "Candidate",
                Id = a.CandidateId,
                Title = a.Candidate.AppUser.DisplayName,
                Subtitle = a.Project.Name,
                Url = "/candidates",
            });

    private static bool Matches(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
