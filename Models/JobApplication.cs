using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpportunityHub.Models;

public class JobApplication
{
    [Key]
    public int Id { get; set; }

    // FK to existing Opportunity (acts as Job)
    [Required]
    public int OpportunityId { get; set; }

    [ForeignKey(nameof(OpportunityId))]
    public Opportunity? Opportunity { get; set; }

    // Candidate (ApplicationUser) who applied
    [Required]
    public string CandidateId { get; set; } = string.Empty;

    [ForeignKey(nameof(CandidateId))]
    public ApplicationUser? Candidate { get; set; }

    // Snapshot fields — kept for convenience and history
    [Required, StringLength(120)]
    public string CandidateName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string CandidateEmail { get; set; } = string.Empty;

    // Use the resume URL from candidate profile at time of application
    public string? ResumeUrl { get; set; }

    // Candidate provided cover letter (optional)
    [StringLength(4000)]
    public string? CoverLetter { get; set; }

    // Application status: Pending, Reviewed, Accepted, Rejected
    [Required, StringLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}
