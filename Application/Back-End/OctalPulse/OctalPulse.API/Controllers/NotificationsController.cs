using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OctalPulse.Application.Features.Notification.DTOs;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IAppNotificationService _notificationService;

    public NotificationsController(IAppNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedNotificationsResponse>> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 8,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await _notificationService.GetNotificationsAsync(
            userId,
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        await _notificationService.MarkAsReadAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new { unreadCount = count });
    }

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
    }
}
