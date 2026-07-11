using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpportunityHub.Data;

namespace OpportunityHub.Services;

/// <summary>
/// Service for calculating unique applicant counts and managing application data integrity.
/// </summary>
public class ApplicantCountService
{
    private readonly ApplicationDbContext _db;

    public ApplicantCountService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Gets the count of unique candidates who have applied for a specific opportunity.
    /// Only counts active applications (not withdrawn, rejected, etc.).
    /// </summary>
    public async Task<int> GetUniqueApplicantCountAsync(int opportunityId)
    {
        return await _db.JobApplications
            .Where(a => a.OpportunityId == opportunityId && a.Status == "Applied")
            .Select(a => a.CandidateId)
            .Distinct()
            .CountAsync();
    }

    /// <summary>
    /// Gets the total count of unique candidates across all applications for a given company.
    /// </summary>
    public async Task<int> GetTotalUniqueApplicantsForCompanyAsync(string company)
    {
        return await _db.JobApplications
            .Where(a => _db.Opportunities.Any(o => o.Id == a.OpportunityId && o.Company == company) && a.Status == "Applied")
            .Select(a => a.CandidateId)
            .Distinct()
            .CountAsync();
    }

    /// <summary>
    /// One-time cleanup: removes duplicate applications, keeping only the earliest for each (CandidateId, OpportunityId) pair.
    /// Also updates Opportunity.Applicants to reflect unique candidate count.
    /// </summary>
    public async Task<(int DuplicatesRemoved, int OpportunitiesUpdated)> CleanupDuplicateApplicationsAsync()
    {
        int duplicatesRemoved = 0;
        int opportunitiesUpdated = 0;

        // Find all (CandidateId, OpportunityId) pairs with more than one application
        var duplicateGroups = await _db.JobApplications
            .GroupBy(a => new { a.CandidateId, a.OpportunityId })
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                g.Key.CandidateId,
                g.Key.OpportunityId,
                Applications = g.OrderBy(a => a.AppliedAt).ToList()
            })
            .ToListAsync();

        // For each group, keep the earliest and remove the rest
        foreach (var group in duplicateGroups)
        {
            var toRemove = group.Applications.Skip(1).ToList();
            _db.JobApplications.RemoveRange(toRemove);
            duplicatesRemoved += toRemove.Count;
        }

        if (duplicatesRemoved > 0)
        {
            await _db.SaveChangesAsync();
        }

        // Update Opportunity.Applicants for all opportunities to reflect unique candidate counts
        var allOpportunities = await _db.Opportunities.ToListAsync();
        foreach (var opp in allOpportunities)
        {
            var uniqueCount = await GetUniqueApplicantCountAsync(opp.Id);
            if (opp.Applicants != uniqueCount)
            {
                opp.Applicants = uniqueCount;
                opportunitiesUpdated++;
            }
        }

        if (opportunitiesUpdated > 0)
        {
            await _db.SaveChangesAsync();
        }

        return (duplicatesRemoved, opportunitiesUpdated);
    }
}