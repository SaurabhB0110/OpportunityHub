using System.Collections.Generic;
using System.Threading.Tasks;
using OpportunityHub.Models;

namespace OpportunityHub.Services;

/// <summary>
/// Interface for notification management service.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates and saves a new notification for a user.
    /// </summary>
    Task<Notification> CreateNotificationAsync(string userId, string title, string message, string type = "Info", string? link = null);

    /// <summary>
    /// Gets paginated notifications for a user ordered by newest first.
    /// </summary>
    Task<(ICollection<Notification> Notifications, int TotalCount)> GetNotificationsAsync(string userId, int pageNumber = 1, int pageSize = 10);

    /// <summary>
    /// Gets the count of unread notifications for a user.
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    Task<bool> MarkAsReadAsync(int notificationId, string userId);

    /// <summary>
    /// Marks all notifications as read for a user.
    /// </summary>
    Task<int> MarkAllAsReadAsync(string userId);

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    Task<bool> DeleteNotificationAsync(int notificationId, string userId);

    /// <summary>
    /// Gets a single notification by ID.
    /// </summary>
    Task<Notification?> GetNotificationByIdAsync(int notificationId, string userId);
}