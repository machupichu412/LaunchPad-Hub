using LaunchPad.Application.Common;
using LaunchPad.Application.Sponsors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Sponsor)]
public class SponsorsController : ControllerBase
{
    private readonly ISponsorRepository _sponsors;
    private readonly ICurrentUser _currentUser;

    public SponsorsController(ISponsorRepository sponsors, ICurrentUser currentUser)
    {
        _sponsors = sponsors;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Resolves the caller's own Sponsor record. Project create/edit endpoints use
    /// this same lookup server-side — the client never supplies a SponsorId directly.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<SponsorDto>> GetMe(CancellationToken ct)
    {
        var sponsor = await _sponsors.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (sponsor is null) return NotFound();

        return Ok(new SponsorDto
        {
            SponsorId = sponsor.SponsorId,
            DisplayName = sponsor.AppUser.DisplayName,
            Organization = sponsor.Organization,
            Title = sponsor.Title,
        });
    }
}
