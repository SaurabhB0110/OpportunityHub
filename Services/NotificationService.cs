using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpportunityHub.Data;
using OpportunityHub.Models;

namespace OpportunityHub.Services;

/// <summary>
/// Service for managing in-app notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext db, ILogger<NotificationService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates and saves a new notification for a user.
    /// </summary>
    public async Task<Notification> CreateNotificationAsync(string userId, string title, string message, string type = "Info", string? link = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Notification created for user {UserId}: {Title}", userId, title);
        return notification;
    }

    /// <summary>
    /// Gets paginated notifications for a user ordered by newest first.
    /// </summary>
    public async Task<(ICollection<Notification> Notifications, int TotalCount)> GetNotificationsAsync(string userId, int pageNumber = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        if (pageNumber < 1)
            pageNumber = 1;

        if (pageSize < 1)
            pageSize = 10;

        var query = _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync();

        var notifications = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (notifications, totalCount);
    }

    /// <summary>
    /// Gets the count of unread notifications for a user.
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        return await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return false;

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Notification {NotificationId} marked as read for user {UserId}", notificationId, userId);
        return true;
    }

    /// <summary>
    /// Marks all notifications as read for a user.
    /// </summary>
    public async Task<int> MarkAllAsReadAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        var count = notifications.Count;
        if (count > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("{Count} notifications marked as read for user {UserId}", count, userId);
        }

        return count;
    }

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    public async Task<bool> DeleteNotificationAsync(int notificationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return false;

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Notification {NotificationId} deleted for user {UserId}", notificationId, userId);
        return true;
    }

    /// <summary>
    /// Gets a single notification by ID.
    /// </summary>
    public async Task<Notification?> GetNotificationByIdAsync(int notificationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

        return await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
    }
}