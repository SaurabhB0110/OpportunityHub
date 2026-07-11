using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace OpportunityHub.Models;

public class ApplicationInput
{
    [Required, StringLength(80)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, Phone]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = string.Empty;

    [Url]
    [Display(Name = "Portfolio or LinkedIn (optional)")]
    public string? PortfolioUrl { get; set; }

    [Required, StringLength(600, MinimumLength = 30)]
    [Display(Name = "Why should we hire you?")]
    public string CoverNote { get; set; } = string.Empty;

    // Resume now required for applications
    [Required]
    [Display(Name = "Upload resume (required)")]
    public IFormFile? Resume { get; set; }
}
