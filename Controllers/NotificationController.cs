using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpportunityHub.Services;
using System.Security.Claims;

namespace OpportunityHub.Controllers;

/// <summary>
/// Controller for managing user notifications.
/// </summary>
[Authorize]
[Route("[controller]")]
public class NotificationController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationService notificationService,
        ILogger<NotificationController> logger)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("User ID not found.");
    }

    /// <summary>
    /// Displays the notification center with paginated notifications.
    /// </summary>
    [HttpGet("Center")]
    public async Task<IActionResult> Center(int page = 1)
    {
        try
        {
            var userId = GetCurrentUserId();
            var (notifications, totalCount) = await _notificationService.GetNotificationsAsync(userId, page, 10);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = (int)Math.Ceiling(totalCount / 10.0);
            ViewData["TotalCount"] = totalCount;
            ViewData["UnreadCount"] = unreadCount;

            return View(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading notification center for user {UserId}", GetCurrentUserId());
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>
    /// Gets the unread notification count for the current user (for AJAX calls).
    /// </summary>
    [HttpGet("UnreadCount")]
    public async Task<IActionResult> UnreadCount()
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            return Json(new { count = 0 });
        }
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    [HttpPost("MarkAsRead")]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _notificationService.MarkAsReadAsync(notificationId, userId);

            if (success)
            {
                _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return Json(new { success = false });
        }
    }

    /// <summary>
    /// Marks all notifications as read for the current user.
    /// </summary>
    [HttpPost("MarkAllAsRead")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = GetCurrentUserId();
            var count = await _notificationService.MarkAllAsReadAsync(userId);
            return Json(new { success = true, count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return Json(new { success = false });
        }
    }

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(int notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);

            if (success)
            {
                _logger.LogInformation("Notification {NotificationId} deleted", notificationId);
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification");
            return Json(new { success = false });
        }
    }
}