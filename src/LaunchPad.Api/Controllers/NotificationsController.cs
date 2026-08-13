using LaunchPad.Application.Common;
using LaunchPad.Application.Notifications;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Controllers;

/// <summary>
/// Every role reads this — it's always scoped to the caller's own AppUserId (resolved
/// server-side, same pattern as everywhere else), never another user's notifications.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private const int RecentTake = 20;

    private readonly INotificationRepository _notifications;
    private readonly IAppUserRepository _appUsers;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(INotificationRepository notifications, IAppUserRepository appUsers, ICurrentUser currentUser)
    {
        _notifications = notifications;
        _appUsers = appUsers;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Get(CancellationToken ct)
    {
        var appUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUserId is null) return Ok(Array.Empty<NotificationDto>());

        var recent = await _notifications.GetRecentForUserAsync(appUserId.Value, RecentTake, ct);
        return Ok(recent.Select(ToDto).ToArray());
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var appUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUserId is null) return Ok(0);

        return Ok(await _notifications.GetUnreadCountAsync(appUserId.Value, ct));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var notification = await _notifications.GetAsync(id, ct);
        if (notification is null) return NotFound();

        var appUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUserId != notification.RecipientAppUserId) return Forbid();

        notification.IsRead = true;
        await _notifications.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var appUserId = await _appUsers.GetIdByEntraObjectIdAsync(_currentUser.EntraObjectId, ct);
        if (appUserId is null) return NoContent();

        await _notifications.MarkAllReadAsync(appUserId.Value, ct);
        await _notifications.SaveChangesAsync(ct);
        return NoContent();
    }

    private static NotificationDto ToDto(Notification notification) => new()
    {
        NotificationId = notification.NotificationId,
        Subject = notification.Subject,
        Body = notification.Body,
        IsRead = notification.IsRead,
        CreatedUtc = notification.CreatedUtc,
    };
}
