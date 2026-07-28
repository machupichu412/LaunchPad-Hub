using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Phase 0 exit criterion: an authenticated user can hit a role-protected endpoint
/// and see their roles echoed back. Useful smoke test once Entra app roles are wired up.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    [HttpGet]
    public ActionResult<MeResponse> Get()
    {
        var response = new MeResponse(
            ObjectId: User.FindFirst("oid")?.Value,
            DisplayName: User.Identity?.Name,
            Roles: User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray());

        return Ok(response);
    }
}

public record MeResponse(string? ObjectId, string? DisplayName, string[] Roles);
