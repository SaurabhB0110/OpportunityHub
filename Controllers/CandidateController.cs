using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using OpportunityHub.Data;
using OpportunityHub.Models;
using OpportunityHub.Services;
using OpportunityHub.ViewModels;

namespace OpportunityHub.Controllers;

[Authorize(Roles = "Candidate")]
public class CandidateController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CandidateController> _logger;
    private readonly INotificationService _notificationService;
    private readonly IS3Service _s3Service;

    private static readonly string[] AllowedResumeExtensions = new[] { ".pdf", ".doc", ".docx" };
    private const long MaxResumeBytes = 5 * 1024 * 1024; // 5 MB

    public CandidateController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IWebHostEnvironment env,
        ILogger<CandidateController> logger,
        INotificationService notificationService,
        IS3Service s3Service)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Include applications with their related opportunities so opportunity details are available in the view
        user = await _db.Users
            .Include(u => u.Applications)
            .ThenInclude(a => a.Opportunity)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var vm = new CandidateEditViewModel
        {
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Skills = user.Skills,
            GraduationDegree = user.GraduationDegree,
            GraduationCollege = user.GraduationCollege,
            GraduationCgpaOrPercentage = user.GraduationCgpaOrPercentage,
            ResumeUrl = user.ResumeUrl
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CandidateEditViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // EMAIL: handle change safely using Identity APIs
        if (!string.Equals(model.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email is required.");
            }
            else
            {
                var existing = await _userManager.FindByEmailAsync(model.Email);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
                }
                else
                {
                    var emailResult = await _userManager.SetEmailAsync(user, model.Email);
                    if (!emailResult.Succeeded)
                    {
                        foreach (var err in emailResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, err.Description);
                        }
                    }
                    else
                    {
                        var userNameResult = await _userManager.SetUserNameAsync(user, model.Email);
                        if (!userNameResult.Succeeded)
                        {
                            foreach (var err in userNameResult.Errors)
                            {
                                ModelState.AddModelError(string.Empty, err.Description);
                            }
                        }
                    }
                }
            }
        }

        // Server-side critical checks: resume must exist (existing or uploaded)
        if (string.IsNullOrWhiteSpace(user.ResumeUrl) && (model.Resume == null || model.Resume.Length == 0))
        {
            ModelState.AddModelError(nameof(model.Resume), "Please upload a resume.");
        }

        if (!ModelState.IsValid) return View(model);

        // Update allowed fields
        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Address = model.Address;
        user.Skills = model.Skills;
        user.GraduationDegree = model.GraduationDegree;
        user.GraduationCollege = model.GraduationCollege;
        user.GraduationCgpaOrPercentage = model.GraduationCgpaOrPercentage;

        // Handle resume upload if provided
        var resume = model.Resume;
        if (resume != null && resume.Length > 0)
        {
            var ext = Path.GetExtension(resume.FileName)?.ToLowerInvariant();
            if (!AllowedResumeExtensions.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.Resume), "Allowed formats: PDF, DOC, DOCX");
                return View(model);
            }

            if (resume.Length > MaxResumeBytes)
            {
                ModelState.AddModelError(nameof(model.Resume), "Resume size must not exceed 5 MB.");
                return View(model);
            }

            try
            {
                // Upload to S3 (folder: resumes)
                var s3Url = await _s3Service.UploadFileAsync(resume, "resumes");
                user.ResumeUrl = s3Url;
                _logger.LogInformation("Resume uploaded to S3 for user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resume upload to S3 failed for user {UserId}", user.Id);
                ModelState.AddModelError(nameof(model.Resume), "Failed to upload resume. Please try again.");
                return View(model);
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            _logger.LogInformation("Candidate {UserId} updated their profile", user.Id);

            // STEP 6: Create Profile Updated notification
            try
            {
                await _notificationService.CreateNotificationAsync(
                    userId: user.Id,
                    title: "Profile Updated",
                    message: "Your profile has been updated successfully.",
                    type: "Success",
                    link: Url.Action("Profile", "Candidate")
                );
                _logger.LogInformation("Profile update notification created for user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create profile update notification for user {UserId}", user.Id);
                // Don't fail the request if notification creation fails
            }

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var err in result.Errors)
        {
            ModelState.AddModelError(string.Empty, err.Description);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> SavedJobs()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Read saved_job claims and build ordered list (newest saved first)
        var claims = await _userManager.GetClaimsAsync(user);
        var savedClaims = claims
            .Where(c => c.Type == "saved_job")
            .Select(c =>
            {
                var parts = (c.Value ?? string.Empty).Split('|');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var jobId) && long.TryParse(parts[1], out var ticks))
                {
                    return new { JobId = jobId, SavedAtTicks = ticks };
                }
                return null;
            })
            .Where(x => x != null)
            .Select(x => new { x!.JobId, SavedAt = new DateTime(x!.SavedAtTicks, DateTimeKind.Utc) })
            .OrderByDescending(x => x.SavedAt)
            .ToList();

        var items = new List<dynamic>();
        foreach (var sc in savedClaims)
        {
            var opp = await _db.Opportunities.FindAsync(sc.JobId);
            // If opp is null (deleted), keep a null opportunity entry and still show remove
            items.Add(new
            {
                Id = sc.JobId,
                Opportunity = opp, // may be null
                SavedAt = sc.SavedAt
            });
        }

        // Pass as dynamic list to view (no new model created)
        return View(items);
    }
}
