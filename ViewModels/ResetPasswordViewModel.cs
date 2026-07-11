using System.ComponentModel.DataAnnotations;

namespace OpportunityHub.ViewModels;

public class ResetPasswordViewModel
{
    public string? UserId { get; set; }

    public string? Code { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}