using System;

namespace OpportunityHub.Models;

public class EmployerProfile
{
    public int Id { get; set; }

    // FK to AspNetUsers.Id
    public string UserId { get; set; } = string.Empty;

    // Navigation to Identity user (optional)
    public ApplicationUser? User { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyAddress { get; set; }
    public string? EmployerId { get; set; }

    // Stored relative url e.g. "/uploads/employers/...."
    public string? VerificationDocumentUrl { get; set; }

    // Verification state managed by SuperAdmin
    public bool IsVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}