using LaunchPad.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Cross-role lookups keyed on the shared AppUser row rather than any one role's profile —
/// currently just the avatar-by-id proxy the Community feed needs, since post/comment authors
/// span Candidate/ProgramOps/Sponsor and none of the existing avatar endpoints
/// (MeController's self-only, CandidatesController's candidate-only) fit that.
/// </summary>
[ApiController]
[Route("api/app-users")]
[Authorize]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserRepository _appUsers;
    private readonly IProfilePictureStorage _profilePictures;

    public AppUsersController(IAppUserRepository appUsers, IProfilePictureStorage profilePictures)
    {
        _appUsers = appUsers;
        _profilePictures = profilePictures;
    }

    /// <summary>Same reasoning as CandidatesController.GetAvatar: a photo carries none of the
    /// hidden-score sensitivity CLAUDE.md's redaction rule is about, so no ownership check
    /// beyond being authenticated at all is needed.</summary>
    [HttpGet("{id:int}/avatar")]
    public async Task<IActionResult> GetAvatar(int id, CancellationToken ct)
    {
        var appUser = await _appUsers.GetByIdAsync(id, ct);
        if (appUser?.AvatarBlobPath is not { } blobPath) return NotFound();

        var result = await _profilePictures.GetAsync(blobPath, ct);
        if (result is null) return NotFound();

        return File(result.Value.Content, result.Value.ContentType);
    }
}
