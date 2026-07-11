using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpportunityHub.Models;

namespace OpportunityHub.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // Persist Opportunities (reusing existing Opportunity model as Job entity)
    public DbSet<Opportunity> Opportunities { get; set; } = null!;

    // Track candidate applications to Opportunities
    public DbSet<JobApplication> JobApplications { get; set; } = null!;

    // Employer verification profiles
    public DbSet<EmployerProfile> EmployerProfiles { get; set; } = null!;

    // In-app notifications
    public DbSet<Notification> Notifications { get; set; } = null!;

    // Optional convenience DbSet for querying users (IdentityDbContext already exposes Users)
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Opportunity -> JobApplication : one-to-many
        builder.Entity<JobApplication>()
            .HasOne(a => a.Opportunity)
            .WithMany() // Opportunity currently does not declare a navigation collection; keep it unidirectional to avoid breaking changes
            .HasForeignKey(a => a.OpportunityId)
            .OnDelete(DeleteBehavior.Cascade);

        // JobApplication -> Candidate (ApplicationUser)
        builder.Entity<JobApplication>()
            .HasOne(a => a.Candidate)
            .WithMany(u => u.Applications) // assumes ApplicationUser.Applications navigation exists (if added)
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        // EmployerProfile -> ApplicationUser : one-to-one (principal: ApplicationUser)
        builder.Entity<EmployerProfile>()
            .HasOne(p => p.User)
            .WithOne() // ApplicationUser does not declare EmployerProfile nav to avoid broad changes
            .HasForeignKey<EmployerProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Notification -> ApplicationUser : many-to-one
        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Notification entity
        builder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Link).HasMaxLength(500);
        });

        // Optional: configure string length / required constraints for snapshot fields
        builder.Entity<JobApplication>(entity =>
        {
            entity.Property(e => e.CandidateName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.CandidateEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CoverLetter).HasMaxLength(4000);
        });
    }
}