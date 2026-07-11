using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpportunityHub.Data;
using Microsoft.AspNetCore.Identity;
using OpportunityHub.Models;
using OpportunityHub.Services;

namespace OpportunityHub.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailService emailService)
    {
        _db = db;
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.TotalEmployers = await _db.EmployerProfiles.CountAsync();
        ViewBag.TotalCandidates = (await _userManager.GetUsersInRoleAsync("Candidate")).Count;
        ViewBag.TotalJobsPosted = await _db.Opportunities.CountAsync();
        ViewBag.PendingEmployers = await _db.EmployerProfiles.Where(p => !p.IsVerified).CountAsync();
        ViewBag.ApprovedEmployers = await _db.EmployerProfiles.Where(p => p.IsVerified).CountAsync();
        ViewBag.ActiveJobs = await _db.Opportunities.Where(o => o.IsActivelyHiring).CountAsync();
        ViewBag.JobApplications = await _db.JobApplications.CountAsync();
        
        return View();
    }

    // List pending employer profiles for verification workflow
    [HttpGet]
    public async Task<IActionResult> PendingEmployers()
    {
        var pending = await _db.EmployerProfiles
            .Include(p => p.User)
            .Where(p => !p.IsVerified)
            .ToListAsync();

        return View(pending);
    }

    // Approve employer (POST) - Marks employer as verified
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveEmployer(int id, string? search, string? filter, int page = 1)
    {
        var profile = await _db.EmployerProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (profile == null) return NotFound();

        profile.IsVerified = true;
        await _db.SaveChangesAsync();

        // Send approval email to employer (best-effort)
        try
        {
            var to = profile.User?.Email;
            if (!string.IsNullOrWhiteSpace(to))
            {
                var subject = "Employer profile approved - OpportunityHub";
                var body = $@"
                    <h2>Your employer profile has been approved</h2>
                    <p>Congratulations — your company <strong>{profile.CompanyName}</strong> has been verified by OpportunityHub administration.</p>
                    <p>You can now post jobs and access employer features in your account.</p>
                    <p>Best regards,<br/>OpportunityHub Team</p>";
                await _emailService.SendEmailAsync(to, subject, body);
            }
        }
        catch
        {
            // non-fatal; admin panel should not fail if email cannot be sent
        }

        return RedirectToAction(nameof(ManageEmployers), new { search, filter, page });
    }

    // Reject employer (POST) - Marks employer as not verified
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectEmployer(int id, string? search, string? filter, int page = 1)
    {
        var profile = await _db.EmployerProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (profile == null) return NotFound();

        profile.IsVerified = false;
        await _db.SaveChangesAsync();

        // Send rejection email to employer (best-effort)
        try
        {
            var to = profile.User?.Email;
            if (!string.IsNullOrWhiteSpace(to))
            {
                var subject = "Employer profile update - OpportunityHub";
                var body = $@"
                    <h2>Your employer profile requires further action</h2>
                    <p>Your submission for <strong>{profile.CompanyName}</strong> could not be approved. Please review the verification document and re-submit or contact support for details.</p>
                    <p>Best regards,<br/>OpportunityHub Team</p>";
                await _emailService.SendEmailAsync(to, subject, body);
            }
        }
        catch
        {
            // non-fatal
        }

        return RedirectToAction(nameof(ManageEmployers), new { search, filter, page });
    }

    // Manage Employers - Display all employers with enable/disable and approval options
    [HttpGet]
    public async Task<IActionResult> ManageEmployers(string? search, string? filter, int page = 1)
    {
        // Build query for all employers (verified and pending)
        IQueryable<EmployerProfile> query = _db.EmployerProfiles.Include(p => p.User);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim().ToLower();
            query = query.Where(p => 
                p.CompanyName.ToLower().Contains(searchTerm) ||
                p.User!.Email!.ToLower().Contains(searchTerm));
        }

        // Apply verification filter
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter == "verified")
                query = query.Where(p => p.IsVerified);
            else if (filter == "pending")
                query = query.Where(p => !p.IsVerified);
        }

        // Sort by creation date descending
        query = query.OrderByDescending(p => p.CreatedAt);

        // Pagination
        const int pageSize = 10;
        var totalRecords = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var employers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get job counts for each employer company
        var companyNames = employers.Select(e => e.CompanyName).Distinct().ToList();
        var jobCounts = await _db.Opportunities
            .Where(o => companyNames.Contains(o.Company))
            .GroupBy(o => o.Company)
            .ToDictionaryAsync(g => g.Key, g => g.Count());

        ViewBag.Search = search;
        ViewBag.Filter = filter;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalRecords = totalRecords;
        ViewBag.JobCounts = jobCounts;

        return View(employers);
    }

    // Toggle employer account (enable/disable via LockoutEnd)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmployerStatus(int id, string? search, string? filter, int page = 1)
    {
        var profile = await _db.EmployerProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profile == null || profile.User == null)
            return NotFound();

        var user = profile.User;

        // Toggle: if LockoutEnd is null or in past, lock the account; otherwise unlock
        if (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow)
        {
            // Lock the account (disable) - set LockoutEnd to far future
            user.LockoutEnd = DateTimeOffset.MaxValue;
            TempData["Success"] = $"Employer account has been disabled.";
        }
        else
        {
            // Unlock the account (enable)
            user.LockoutEnd = null;
            TempData["Success"] = $"Employer account has been enabled.";
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "Failed to update employer account status.";
        }

        return RedirectToAction(nameof(ManageEmployers), new { search, filter, page });
    }

    [HttpGet]
    public async Task<IActionResult> ManageCandidates()
    {
        var candidates = await _userManager.GetUsersInRoleAsync("Candidate");
        return View(candidates);
    }

    // View employer profile (read-only for SuperAdmin)
    [HttpGet]
    public async Task<IActionResult> ViewEmployerProfile(int id)
    {
        var profile = await _db.EmployerProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profile == null || profile.User == null)
            return NotFound();

        // Return as Tuple to match EmployerController CompanyProfile view model pattern
        var model = Tuple.Create(profile.User, profile);
        return View("~/Views/Employer/CompanyProfile.cshtml", model);
    }
}