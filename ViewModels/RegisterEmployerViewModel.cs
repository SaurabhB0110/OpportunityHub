using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace OpportunityHub.ViewModels;

public class RegisterEmployerViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Organization / Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Company address")]
    public string? CompanyAddress { get; set; }

    [Display(Name = "Company description")]
    public string? CompanyDescription { get; set; }

    [Display(Name = "Website")]
    [Url]
    public string? Website { get; set; }

    [Required]
    [Phone]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [Display(Name = "Employer ID (optional)")]
    public string? EmployerId { get; set; }

    [Display(Name = "Verification document (PDF/JPG/PNG)")]
    public IFormFile? VerificationDocument { get; set; }
}
