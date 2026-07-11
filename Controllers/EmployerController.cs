using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OpportunityHub.Data;
using OpportunityHub.Models;
using OpportunityHub.Services;

namespace OpportunityHub.Controllers;

[Authorize(Roles = "Employer")]
public class EmployerController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmployerController> _logger;
    private readonly ApplicantCountService _applicantCountService;
    private readonly INotificationService _notificationService;
    private readonly IS3Service _s3Service;

    private static readonly string[] AllowedResumeExtensions = new[] { ".pdf", ".doc", ".docx" };
    private const long MaxResumeBytes = 5 * 1024 * 1024; // 5 MB

    public EmployerController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env,
        ILogger<EmployerController> logger,
        ApplicantCountService applicantCountService,
        INotificationService notificationService,
        IS3Service s3Service)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _applicantCountService = applicantCountService ?? throw new ArgumentNullException(nameof(applicantCountService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var company = user.CompanyName ?? string.Empty;

        // Total jobs posted
        var totalJobs = await _db.Opportunities.CountAsync(o => o.Company == company);

        // Applications metrics for this company (use Opportunity relationship via Id check)
        var totalApplications = await _db.JobApplications.CountAsync(a =>
            _db.Opportunities.Any(o => o.Id == a.OpportunityId && o.Company == company));

        var pendingApplications = await _db.JobApplications.CountAsync(a =>
            a.Status == "Applied" &&
            _db.Opportunities.Any(o => o.Id == a.OpportunityId && o.Company == company));

        var shortlistedApplications = await _db.JobApplications.CountAsync(a =>
            a.Status == "Shortlisted" &&
            _db.Opportunities.Any(o => o.Id == a.OpportunityId && o.Company == company));

        var hiredCandidates = await _db.JobApplications.CountAsync(a =>
            a.Status == "Hired" &&
            _db.Opportunities.Any(o => o.Id == a.OpportunityId && o.Company == company));

        // Keep existing unique applicants metric (service)
        var totalApplicants = await _applicantCountService.GetTotalUniqueApplicantsForCompanyAsync(company);

        // Get recent applications for the model
        var recentApplications = await _db.JobApplications
            .Include(a => a.Opportunity)
            .Where(a => _db.Opportunities.Any(o => o.Id == a.OpportunityId && o.Company == company))
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();

        // Pass statistics to ViewBag
        ViewBag.TotalJobs = totalJobs;
        ViewBag.TotalApplications = totalApplications;
        ViewBag.PendingApplications = pendingApplications;
        ViewBag.ShortlistedApplications = shortlistedApplications;
        ViewBag.HiredCandidates = hiredCandidates;
        ViewBag.TotalApplicants = totalApplicants;

        // Pass recent applications as strongly-typed model
        return View(recentApplications);
    }

    [HttpGet]
    public async Task<IActionResult> Jobs()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var company = user.CompanyName ?? string.Empty;

        // Load jobs for the company
        var jobs = await _db.Opportunities.Where(j => j.Company == company).ToListAsync();

        if (jobs.Any())
        {
            // Use the exact FK defined on JobApplication (OpportunityId) to count applications
            var jobIds = jobs.Select(j => j.Id).ToList();
            var counts = await _db.JobApplications
                .Where(a => jobIds.Contains(a.OpportunityId))
                .GroupBy(a => a.OpportunityId)
                .Select(g => new { OpportunityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OpportunityId, x => x.Count);

            // Populate the existing Applicants property on each Opportunity model
            foreach (var job in jobs)
            {
                counts.TryGetValue(job.Id, out var c);
                job.Applicants = c;
            }
        }

        // Return the existing MyJobs view
        return View("MyJobs", jobs);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var job = await _db.Opportunities.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null) return NotFound();

        if (job.Company != user.CompanyName) return Forbid();

        return View(job);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateApplicationStatus(int applicationId, string status)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var app = await _db.JobApplications
            .Include(a => a.Opportunity)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null) return NotFound();

        if (app.Opportunity?.Company != user.CompanyName)
            return Forbid();

        var validStatuses = new[] { "Applied", "Shortlisted", "Hired", "Rejected" };
        if (!validStatuses.Contains(status))
            return BadRequest("Invalid status");

        app.Status = status;
        await _db.SaveChangesAsync();

        // STEP 6: Create candidate notifications based on status change
        try
        {
            if (status == "Hired")
            {
                await _notificationService.CreateNotificationAsync(
                    userId: app.CandidateId,
                    title: "Application Accepted",
                    message: "Congratulations! Your application has been accepted.",
                    type: "Success",
                    link: Url.Action("Dashboard", "Candidate")
                );
                _logger.LogInformation("Application accepted notification created for candidate {CandidateId}", app.CandidateId);
            }
            else if (status == "Rejected")
            {
                await _notificationService.CreateNotificationAsync(
                    userId: app.CandidateId,
                    title: "Application Update",
                    message: "Unfortunately your application was not selected.",
                    type: "Warning",
                    link: Url.Action("Dashboard", "Candidate")
                );
                _logger.LogInformation("Application rejected notification created for candidate {CandidateId}", app.CandidateId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification for status change to {Status} for candidate {CandidateId}", status, app.CandidateId);
            // Don't fail the request if notification creation fails
        }

        TempData["Success"] = $"Application status updated to {status}.";
        return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
    }

    // Step 9: Dedicated actions for Shortlist, Hire, Reject
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ShortlistApplicant(int applicationId, int jobId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var app = await _db.JobApplications
            .Include(a => a.Opportunity)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
        {
            TempData["Error"] = "Application not found.";
            return RedirectToAction(nameof(Applicants), new { id = jobId });
        }

        if (app.Opportunity?.Company != user.CompanyName)
        {
            TempData["Error"] = "You are not authorized to update this application.";
            return RedirectToAction(nameof(Applicants), new { id = jobId });
        }

        if (app.Status == "Shortlisted")
        {
            TempData["Error"] = "Applicant is already shortlisted.";
            return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
        }

        app.Status = "Shortlisted";
        await _db.SaveChangesAsync();

        TempData["Success"] = "Applicant shortlisted.";
        return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HireApplicant(int applicationId, int jobId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var app = await _db.JobApplications
            .Include(a => a.Opportunity)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
        {
            TempData["Error"] = "Application not found.";
            return RedirectToAction(nameof(Applicants), new { id = jobId });
        }

        if (app.Opportunity?.Company != user.CompanyName)
        {
            TempData["Error"] = "You are not authorized to update this application.";
            return RedirectToAction(nameof(Applicants), new { id = jobId });
        }

        if (app.Status == "Hired")
        {
            TempData["Error"] = "Applicant is already marked as hired.";
            return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
        }

        app.Status = "Hired";
        await _db.SaveChangesAsync();

        // STEP 6: Create Application Accepted notification
        try
        {
            await _notificationService.CreateNotificationAsync(
                userId: app.CandidateId,
                title: "Application Accepted",
                message: "Congratulations! Your application has been accepted.",
                type: "Success",
                link: Url.Action("Dashboard", "Candidate")
            );
            _logger.LogInformation("Application accepted notification created for candidate {CandidateId}", app.CandidateId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create acceptance notification for candidate {CandidateId}", app.CandidateId);
            // Don't fail the request if notification creation fails
        }

        TempData["Success"] = "Applicant marked as hired.";
        return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectApplicant(int applicationId, int jobId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var app = await _db.JobApplications
            .Include(a => a.Opportunity)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
        {
            TempData["Error"] = "Application not found.";
            return RedirectToAction(nameof(Applicants), new { id = jobId });
        }

        if (app.Opportunity?.Company != user.CompanyName)
        {
            TempData["Error"] = "You are not authorized to update this application.";
            return RedirectToAction(nameof(Applicants), new { id = jobId });
        }

        if (app.Status == "Rejected")
        {
            TempData["Error"] = "Applicant is already marked as rejected.";
            return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
        }

        app.Status = "Rejected";
        await _db.SaveChangesAsync();

        // STEP 6: Create Application Rejected notification
        try
        {
            await _notificationService.CreateNotificationAsync(
                userId: app.CandidateId,
                title: "Application Update",
                message: "Unfortunately your application was not selected.",
                type: "Warning",
                link: Url.Action("Dashboard", "Candidate")
            );
            _logger.LogInformation("Application rejected notification created for candidate {CandidateId}", app.CandidateId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create rejection notification for candidate {CandidateId}", app.CandidateId);
            // Don't fail the request if notification creation fails
        }

        TempData["Success"] = "Applicant marked as rejected.";
        return RedirectToAction(nameof(Applicants), new { id = app.OpportunityId });
    }

    [HttpGet]
    public async Task<IActionResult> Applicants(int id, string? search, string? status = "", string? fromDate = null, string? toDate = null, string sort = "newest", int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var opp = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == id && o.Company == user.CompanyName);
        if (opp == null) return NotFound();

        // STEP 1: Base query
        IQueryable<JobApplication> query = _db.JobApplications
            .Where(a => a.OpportunityId == id)
            .Include(a => a.Candidate);

        // STEP 2: Filters

        // Search (EF Core compatible using EF.Functions.Like)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.CandidateName, $"%{s}%") ||
                EF.Functions.Like(a.CandidateEmail, $"%{s}%"));
        }

        // Status: only filter when a specific status is selected (non-empty)
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var from))
        {
            query = query.Where(a => a.AppliedAt >= from);
        }

        if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var to))
        {
            query = query.Where(a => a.AppliedAt <= to.AddDays(1));
        }

        // STEP 3: Sorting
        query = sort switch
        {
            "oldest" => query.OrderBy(a => a.AppliedAt),
            "name_asc" => query.OrderBy(a => a.CandidateName),
            "name_desc" => query.OrderByDescending(a => a.CandidateName),
            _ => query.OrderByDescending(a => a.AppliedAt)
        };

        // STEP 4: Assign view data
        const int pageSize = 10;

        // STEP 5: Pagination
        var totalRecords = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

        // Ensure page is valid
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var applicants = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["JobTitle"] = opp.Title;
        ViewData["JobId"] = id;
        ViewData["Search"] = search;
        ViewData["StatusFilter"] = status;
        ViewData["FromDate"] = fromDate;
        ViewData["ToDate"] = toDate;
        ViewData["Sort"] = sort;
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalRecords"] = totalRecords;
        ViewData["PageSize"] = pageSize;

        return View(applicants);
    }

    [HttpGet]
    public async Task<IActionResult> CompanyProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var profile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile == null)
        {
            // create an in-memory profile object; persist only on POST
            profile = new EmployerProfile
            {
                UserId = user.Id,
                CompanyName = user.CompanyName ?? string.Empty,
                CompanyAddress = string.Empty,
                EmployerId = string.Empty,
                VerificationDocumentUrl = null,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        // Use Tuple to avoid introducing a new ViewModel file
        var model = Tuple.Create(user, profile);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompanyProfile(
        string companyName,
        string? companyDescription,
        string? website,
        string? contactNumber,
        string? companyAddress,
        string? employerId,
        IFormFile? logo)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Basic server-side validation
        if (string.IsNullOrWhiteSpace(companyName))
        {
            ModelState.AddModelError(nameof(companyName), "Company name is required.");
        }

        if (!ModelState.IsValid)
        {
            // Rebuild profile for view with current posted values
            var existingProfile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                ?? new EmployerProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };

            existingProfile.CompanyName = companyAddress ?? existingProfile.CompanyName;
            existingProfile.CompanyAddress = companyAddress ?? existingProfile.CompanyAddress;
            existingProfile.EmployerId = employerId ?? existingProfile.EmployerId;

            // Return the tuple view model with posted values reflected
            user.CompanyName = companyName;
            user.CompanyDescription = companyDescription ?? user.CompanyDescription;
            user.Website = website ?? user.Website;
            user.ContactNumber = contactNumber ?? user.ContactNumber;

            var vm = Tuple.Create(user, existingProfile);
            return View(vm);
        }

        try
        {
            // Update ApplicationUser fields (persisted via Identity)
            user.CompanyName = companyName.Trim();
            user.CompanyDescription = companyDescription?.Trim();
            user.Website = website?.Trim();
            user.ContactNumber = contactNumber?.Trim();

            var userUpdateResult = await _userManager.UpdateAsync(user);
            if (!userUpdateResult.Succeeded)
            {
                foreach (var err in userUpdateResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                var existingProfile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                    ?? new EmployerProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };

                var vm = Tuple.Create(user, existingProfile);
                return View(vm);
            }

            // EmployerProfile: upsert
            var profile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new EmployerProfile
                {
                    UserId = user.Id,
                    CompanyName = companyName.Trim(),
                    CompanyAddress = companyAddress?.Trim() ?? string.Empty,
                    EmployerId = employerId?.Trim() ?? string.Empty,
                    IsVerified = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.EmployerProfiles.Add(profile);
            }
            else
            {
                profile.CompanyName = companyName.Trim();
                profile.CompanyAddress = companyAddress?.Trim() ?? profile.CompanyAddress;
                profile.EmployerId = employerId?.Trim() ?? profile.EmployerId;
            }

            // Handle logo upload if provided -> upload to S3 under company-logos/
            if (logo != null && logo.Length > 0)
            {
                var ext = Path.GetExtension(logo.FileName)?.ToLowerInvariant() ?? "";
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".svg" };
                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError(nameof(logo), "Allowed logo formats: png, jpg, jpeg, gif, svg");
                    var vm = Tuple.Create(user, profile);
                    return View(vm);
                }

                try
                {
                    var s3Url = await _s3Service.UploadFileAsync(logo, "company-logos");
                    profile.VerificationDocumentUrl = s3Url;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Logo upload to S3 failed for user {UserId}", user.Id);
                    ModelState.AddModelError(nameof(logo), "Failed to upload logo. Please try again.");
                    var vm = Tuple.Create(user, profile);
                    return View(vm);
                }
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Company profile saved.";
            return RedirectToAction(nameof(CompanyProfile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save company profile for user {UserId}", user.Id);
            TempData["Error"] = "An error occurred while saving your profile. Please try again.";
            var existingProfile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                ?? new EmployerProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };
            var vm = Tuple.Create(user, existingProfile);
            return View(vm);
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        // Reuse the existing Razor view at Views/Employer/Create.cshtml
        return View("Create");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Basic model validation; if invalid, return the same view with model errors
        if (!ModelState.IsValid)
        {
            return View("Create", opportunity);
        }

        // Ensure server-controlled fields are set
        opportunity.Company = user.CompanyName ?? string.Empty;
        opportunity.PostedAt = DateTime.UtcNow;
        opportunity.Applicants = 0;
        opportunity.IsActivelyHiring = opportunity.IsActivelyHiring; // preserve posted value

        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Opportunity posted.";
        return RedirectToAction(nameof(Jobs));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var job = await _db.Opportunities.FindAsync(id);
        if (job == null) return NotFound();

        // Only the owning company may delete this job
        if (job.Company != user.CompanyName) return Forbid();

        // Remove dependent job applications first (avoids FK constraint errors),
        // then remove the opportunity.
        var apps = _db.JobApplications.Where(a => a.OpportunityId == id);
        _db.JobApplications.RemoveRange(apps);
        _db.Opportunities.Remove(job);

        await _db.SaveChangesAsync();

        TempData["Success"] = "Job deleted.";
        return RedirectToAction(nameof(Jobs));
    }
}
