using System;
using System.ComponentModel.DataAnnotations;

namespace OpportunityHub.Models;

public class CreateOpportunityInput
{
    [Required, StringLength(100)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(80)] [Display(Name = "Company name")] public string Company { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Location { get; set; } = string.Empty;
    [Required] [Display(Name = "Work mode")] public string WorkMode { get; set; } = "Remote";
    [Required] [Display(Name = "Opportunity type")] public string Type { get; set; } = "Internship";
    [Required] public string Category { get; set; } = "Software Development";
    [Required, StringLength(60)] [Display(Name = "Salary / stipend")] public string Compensation { get; set; } = string.Empty;
    [Required, StringLength(60)] public string Experience { get; set; } = string.Empty;
    [Required, StringLength(1200, MinimumLength = 50)] public string Description { get; set; } = string.Empty;

    [Required, StringLength(300)]
    [Display(Name = "Skills")]
    public string Skills { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    [Display(Name = "Responsibilities")]
    public string? Responsibilities { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    [Display(Name = "Requirements")]
    public string? Requirements { get; set; } = string.Empty;

    [Required] [DataType(DataType.Date)] [Display(Name = "Apply by")] public DateTime ApplyBy { get; set; } = DateTime.Today.AddDays(30);
}
