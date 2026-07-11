using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace OpportunityHub.Models;

public class ApplicationUser : IdentityUser
{
    // Candidate profile
    public string? FullName { get; set; }
    public int? Age { get; set; }

    // Education details
    public string? TenthSchool { get; set; }
    public string? TenthPercentage { get; set; }
    public string? TwelfthCollege { get; set; }
    public string? TwelfthPercentage { get; set; }
    public string? GraduationDegree { get; set; }    // e.g., B.Sc / B.Tech / Diploma
    public string? GraduationCollege { get; set; }
    public string? GraduationCgpaOrPercentage { get; set; }

    public string? Address { get; set; }
    public string? Skills { get; set; }
    public string? Projects { get; set; }
    public string? ResumeUrl { get; set; }

    // New: Experience (free text)
    public string? Experience { get; set; }

    // Employer verification stub fields (existing)
    public string? CompanyName { get; set; }
    public string? EmployerId { get; set; } // employer's internal id
    public bool IsEmployerVerified { get; set; }
    public string? VerificationDocumentUrl { get; set; }
    public DateTime? VerificationRequestedAt { get; set; }

    // New employer additional fields (stored on AspNetUsers)
    public string? CompanyDescription { get; set; }
    public string? Website { get; set; }
    public string? ContactNumber { get; set; }

    // Navigation property for related JobApplications
    public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
}