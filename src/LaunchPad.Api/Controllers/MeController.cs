using LaunchPad.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Phase 0 exit criterion: an authenticated user can hit a role-protected endpoint
/// and see their roles echoed back. Useful smoke test once Entra app roles are wired up.
/// Also home to the profile-picture endpoints — every role reaches this controller,
/// and a photo belongs to the shared AppUser row, not any one role's profile.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    // 2 MB — the client crops/exports a small square before uploading (see
    // AvatarEditorDialog.tsx), so this is a generous server-side backstop, not the
    // expected size.
    private const long MaxAvatarBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };

    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IProfilePictureStorage _profilePictures;

    public MeController(IAppUserRepository appUsers, ICurrentUser currentUser, IProfilePictureStorage profilePictures)
    {
        _appUsers = appUsers;
        _currentUser = currentUser;
        _profilePictures = profilePictures;
    }

    [HttpGet]
    public ActionResult<MeResponse> Get()
    {
        var response = new MeResponse(
            ObjectId: User.GetObjectId(),
            DisplayName: User.Identity?.Name,
            Roles: User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray());

        return Ok(response);
    }

    /// <summary>Replaces the caller's photo. Body is the raw image bytes; Content-Type
    /// identifies the format — no multipart wrapper needed since the client already
    /// has a single cropped Blob to send (see AvatarEditorDialog.tsx).</summary>
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(CancellationToken ct)
    {
        var contentType = Request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedAvatarContentTypes.Contains(contentType))
        {
            return BadRequest("Unsupported image type. Use JPEG, PNG, or WebP.");
        }

        if (Request.ContentLength is > MaxAvatarBytes)
        {
            return BadRequest("Image is too large. Maximum size is 2 MB.");
        }

        var appUser = await _appUsers.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUser is null) return Forbid();

        using var buffer = new MemoryStream();
        // Content-Length can be absent or spoofed — cap the actual bytes read too.
        if (!await TryCopyWithLimitAsync(Request.Body, buffer, MaxAvatarBytes, ct))
        {
            return BadRequest("Image is too large. Maximum size is 2 MB.");
        }
        buffer.Position = 0;

        var previousBlobPath = appUser.AvatarBlobPath;
        appUser.AvatarBlobPath = await _profilePictures.SaveAsync(appUser.AppUserId, buffer, contentType, ct);
        await _appUsers.SaveChangesAsync(ct);

        if (previousBlobPath is not null)
        {
            await _profilePictures.DeleteAsync(previousBlobPath, ct);
        }

        return NoContent();
    }

    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar(CancellationToken ct)
    {
        var appUser = await _appUsers.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUser?.AvatarBlobPath is null) return NotFound();

        var result = await _profilePictures.GetAsync(appUser.AvatarBlobPath, ct);
        if (result is null) return NotFound();

        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct)
    {
        var appUser = await _appUsers.GetByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUser?.AvatarBlobPath is null) return NoContent();

        await _profilePictures.DeleteAsync(appUser.AvatarBlobPath, ct);
        appUser.AvatarBlobPath = null;
        await _appUsers.SaveChangesAsync(ct);

        return NoContent();
    }

    private static async Task<bool> TryCopyWithLimitAsync(Stream source, Stream destination, long limit, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > limit) return false;
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return true;
    }
}

public record MeResponse(string? ObjectId, string? DisplayName, string[] Roles);
