using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using OpportunityHub.Data;
using OpportunityHub.Models;
using OpportunityHub.Services;

namespace OpportunityHub.Controllers;

[Authorize]
public class ApplicationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly ApplicantCountService _applicantCountService;

    public ApplicationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, ApplicantCountService applicantCountService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _applicantCountService = applicantCountService ?? throw new ArgumentNullException(nameof(applicantCountService));
    }

    // GET: /Applications/MyApplications
    // Added search, status filter, sort and pagination (default pageSize = 10).
    [Authorize(Roles = "Candidate")]
    [HttpGet]
    public async Task<IActionResult> MyApplications(string? q, string status = "All", string sort = "latest", int page = 1, int pageSize = 10)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Base query for this candidate including Opportunity navigation
        var query = _db.JobApplications
            .Where(a => a.CandidateId == user.Id)
            .Include(a => a.Opportunity)
            .AsQueryable();

        // Search by Job Title or Company
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a =>
                (a.Opportunity != null && a.Opportunity.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (a.Opportunity != null && a.Opportunity.Company.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.Status == status);
        }

        // Sorting
        query = (sort ?? "latest") switch
        {
            "oldest" => query.OrderBy(a => a.AppliedAt),
            _ => query.OrderByDescending(a => a.AppliedAt),
        };

        // Pagination
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Pass paging and filter state to the view via ViewData (no new viewmodels created)
        ViewData["Query"] = q ?? string.Empty;
        ViewData["Status"] = status ?? "All";
        ViewData["Sort"] = sort ?? "latest";
        ViewData["Page"] = page;
        ViewData["PageSize"] = pageSize;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalCount"] = totalCount;

        return View(items);
    }

    // POST: /Applications/Withdraw
    // Do NOT delete the application. Mark status as "Withdrawn" and update counts.
    [Authorize(Roles = "Candidate")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int applicationId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var application = await _db.JobApplications
            .Include(a => a.Opportunity)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null) return NotFound();

        // Authorization: only the candidate who submitted the application can withdraw it
        if (application.CandidateId != user.Id) return Forbid();

        // Only allow withdraw for specific statuses
        if (application.Status == "Withdrawn" || application.Status == "Rejected" || application.Status == "Hired")
        {
            TempData["Error"] = "This application cannot be withdrawn.";
            return RedirectToAction(nameof(MyApplications));
        }

        var opportunityId = application.OpportunityId;

        // Mark as withdrawn (do not delete)
        application.Status = "Withdrawn";
        _db.JobApplications.Update(application);
        await _db.SaveChangesAsync();

        // Update applicant count for the job posting to reflect unique candidate count
        try
        {
            var opportunity = await _db.Opportunities.FindAsync(opportunityId);
            if (opportunity != null)
            {
                opportunity.Applicants = await _applicantCountService.GetUniqueApplicantCountAsync(opportunityId);
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the withdrawal if applicant count update fails
            System.Diagnostics.Debug.WriteLine($"Failed to update applicant count: {ex.Message}");
        }

        TempData["Success"] = "Application withdrawn successfully.";
        return RedirectToAction(nameof(MyApplications));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> DownloadResume(int applicationId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var app = await _db.JobApplications.FindAsync(applicationId);
        if (app == null) return NotFound();

        // Candidates can download their own, employers via EmployerController
        if (app.CandidateId != user.Id && !User.IsInRole("Employer"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(app.ResumeUrl)) return NotFound();

        return Redirect(app.ResumeUrl);
    }
}