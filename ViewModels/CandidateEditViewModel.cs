using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace OpportunityHub.ViewModels;

public class CandidateEditViewModel
{
    [Required, StringLength(120)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, Phone]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required]
    [Display(Name = "Address / City")]
    public string? Address { get; set; }

    [Required]
    [Display(Name = "Skills")]
    public string? Skills { get; set; }

    [Required]
    [Display(Name = "Graduation Degree")]
    public string? GraduationDegree { get; set; }

    [Required]
    [Display(Name = "Graduation College")]
    public string? GraduationCollege { get; set; }

    [Required]
    [Display(Name = "CGPA / Percentage")]
    public string? GraduationCgpaOrPercentage { get; set; }

    // Resume upload for profile editing
    [Display(Name = "Upload resume (PDF/DOC)")]
    public IFormFile? Resume { get; set; }

    // Current resume url (read-only for the view)
    public string? ResumeUrl { get; set; }
}