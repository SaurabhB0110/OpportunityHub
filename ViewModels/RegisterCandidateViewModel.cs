using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace OpportunityHub.ViewModels;

public class RegisterCandidateViewModel
{
    // Personal
    [Required]
    [StringLength(120)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Phone number")]
    public string? Phone { get; set; }

    public string? City { get; set; }

    [Range(16, 120)]
    public int? Age { get; set; }

    public string? Address { get; set; }

    // Education
    [Display(Name = "10th School")]
    public string? TenthSchool { get; set; }

    [Display(Name = "10th Percentage")]
    public string? TenthPercentage { get; set; }

    [Display(Name = "12th College")]
    public string? TwelfthCollege { get; set; }

    [Display(Name = "12th Percentage")]
    public string? TwelfthPercentage { get; set; }

    [Display(Name = "Graduation Degree")]
    public string? GraduationDegree { get; set; }

    [Display(Name = "Graduation College")]
    public string? GraduationCollege { get; set; }

    [Display(Name = "Graduation CGPA / Percentage")]
    public string? GraduationCgpaOrPercentage { get; set; }

    // Professional
    public string? Skills { get; set; }
    public string? Projects { get; set; }

    [Display(Name = "Experience")]
    public string? Experience { get; set; }

    // Documents
    [Display(Name = "Upload Resume (PDF/DOC)")]
    public IFormFile? Resume { get; set; }
}