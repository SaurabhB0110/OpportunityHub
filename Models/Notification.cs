using System;

namespace OpportunityHub.Models;

/// <summary>
/// Represents an in-app notification for a user.
/// </summary>
public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Type { get; set; } = "Info"; // Success, Info, Warning, Error
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Link { get; set; }

    // Navigation property
    public ApplicationUser? User { get; set; }
}