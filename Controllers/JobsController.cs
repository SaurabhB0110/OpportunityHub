using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using OpportunityHub.Models;
using OpportunityHub.Services;
using OpportunityHub.Data;

namespace OpportunityHub.Controllers;

public class JobsController : Controller
{
    private readonly IOpportunityRepository _repository;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<JobsController> _logger;
    private readonly ApplicantCountService _applicantCountService;
    private readonly INotificationService _notification_service;
    private readonly IS3Service _s3Service;

    public JobsController(
        IOpportunityRepository repository,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env,
        ILogger<JobsController> logger,
        ApplicantCountService applicantCountService,
        INotificationService notificationService,
        IS3Service s3Service)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _applicantCountService = applicantCountService ?? throw new ArgumentNullException(nameof(applicantCountService));
        _notification_service = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
    }

    public IActionResult Index(string? q, string? location, string? workMode, string? type, string? category, string sort = "newest")
    {
        var items = _repository.GetAll().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
            items = items.Where(x => (x.Title ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || (x.Company ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                                 || (x.Skills ?? new List<string>()).Any(s => s.Contains(q, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(location))
            items = items.Where(x => (x.Location ?? string.Empty).Contains(location, StringComparison.OrdinalIgnoreCase)
                                 || (x.WorkMode ?? string.Empty).Contains(location, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(workMode))
            items = items.Where(x => (x.WorkMode ?? string.Empty).Contains(workMode, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(type))
            items = items.Where(x => (x.Type ?? string.Empty).Contains(type, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category))
            items = items.Where(x => (x.Category ?? string.Empty).Contains(category, StringComparison.OrdinalIgnoreCase));

        items = sort switch
        {
            "oldest" => items.OrderBy(x => x.PostedAt),
            _ => items.OrderByDescending(x => x.PostedAt)
        };

        var viewModel = new BrowseViewModel
        {
            Opportunities = items.ToList(),
            Query = q,
            Location = location,
            WorkMode = workMode,
            Type = type,
            Category = category,
            Sort = sort
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = _repository.GetById(id);
        if (item == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var hasApplied = false;
        var isSaved = false;

        if (user != null)
        {
            // Prevent duplicate - check any existing application for this candidate + opportunity (any status)
            hasApplied = await _db.JobApplications.AnyAsync(a => a.OpportunityId == id && a.CandidateId == user.Id);

            // Determine if this job is saved by the currently logged in candidate (read claims)
            try
            {
                var claims = await _userManager.GetClaimsAsync(user);
                isSaved = claims.Any(c => c.Type == "saved_job" && c.Value.StartsWith($"{id}|"));
            }
            catch (Exception ex)
            {
                // Log but don't fail page render
                _logger.LogWarning(ex, "Failed to read saved-job claims for user {UserId}", user.Id);
            }
        }

        ViewData["IsSaved"] = isSaved;

        var viewModel = new OpportunityDetailsViewModel
        {
            Opportunity = item,
            HasApplied = hasApplied,
            Application = new ApplicationInput(),
            Similar = _repository.GetAll().Where(x => x.Id != id && x.Category == item.Category).Take(3).ToList()
        };

        return View(viewModel);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> SaveJob(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var claims = await _userManager.GetClaimsAsync(user);
            // Prevent duplicates
            if (claims.Any(c => c.Type == "saved_job" && c.Value.StartsWith($"{id}|")))
            {
                TempData["Success"] = "Job is already saved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var value = $"{id}|{DateTime.UtcNow.Ticks}";
            var claim = new Claim("saved_job", value);
            var addResult = await _userManager.AddClaimAsync(user, claim);
            if (!addResult.Succeeded)
            {
                _logger.LogWarning("Failed to add saved_job claim for user {UserId}, job {JobId}: {Errors}", user.Id, id, string.Join(", ", addResult.Errors.Select(e => e.Description)));
                TempData["Error"] = "Unable to save job right now.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = "Job saved.";
            _logger.LogInformation("User {UserId} saved job {JobId}", user.Id, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving job {JobId} for user {UserId}", id, user?.Id);
            TempData["Error"] = "Unable to save job right now.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> UnsaveJob(int id, string? returnUrl = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var toRemove = claims.Where(c => c.Type == "saved_job" && c.Value.StartsWith($"{id}|")).ToList();
            var removedAny = false;
            foreach (var c in toRemove)
            {
                var removeResult = await _userManager.RemoveClaimAsync(user, c);
                if (removeResult.Succeeded)
                    removedAny = true;
            }

            TempData["Success"] = removedAny ? "Job removed from saved jobs." : "Job was not in your saved jobs.";
            _logger.LogInformation("User {UserId} unsaved job {JobId} (removed {Count})", user.Id, id, toRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsaving job {JobId} for user {UserId}", id, user?.Id);
            TempData["Error"] = "Unable to remove saved job right now.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int id, OpportunityDetailsViewModel model)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogInformation("Unauthenticated user attempted to apply for job {JobId}", id);
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Jobs", new { id }) });
        }

        var item = _repository.GetById(id);
        if (item == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User is null in Apply for job {JobId}", id);
            TempData["Error"] = "Unable to identify your account. Please log in and try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // SERVER-SIDE: Check for duplicate application before validating form
        var already = await _db.JobApplications.AnyAsync(a => a.OpportunityId == id && a.CandidateId == user.Id);
        if (already)
        {
            _logger.LogInformation("Duplicate application prevented: user {UserId} for job {JobId}", user.Id, id);
            TempData["Error"] = "You have already applied for this job.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ModelState.Remove(nameof(model.Opportunity));
        ModelState.Remove(nameof(model.Similar));

        _logger.LogDebug("ModelState.IsValid = {IsValid}", ModelState.IsValid);
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState invalid applying for job {JobId}: {Errors}", id, string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            model.Opportunity = item;
            model.Similar = _repository.GetAll().Where(x => x.Id != id && x.Category == item.Category).Take(3).ToList();
            model.HasApplied = false;
            ViewData["OpenApplication"] = true;
            return View("Details", model);
        }

        var application = new JobApplication
        {
            OpportunityId = id,
            CandidateId = user.Id,
            CandidateName = string.IsNullOrWhiteSpace(user.FullName) ? model.Application.FullName : user.FullName,
            CandidateEmail = string.IsNullOrWhiteSpace(user.Email) ? model.Application.Email : user.Email,
            CoverLetter = model.Application.CoverNote,
            AppliedAt = DateTime.UtcNow,
            Status = "Applied"
        };

        // Save resume if present in the apply form; fallback to user.ResumeUrl
        try
        {
            string? resumeUrl = null;
            var upload = model.Application?.Resume;
            if (upload != null && upload.Length > 0)
            {
                try
                {
                    // Upload to S3 (folder: resumes) using the shared IS3Service implementation
                    resumeUrl = await _s3Service.UploadFileAsync(upload, "resumes");
                    _logger.LogInformation("Uploaded resume to S3 for user {UserId}: {ResumeUrl}", user.Id, resumeUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "S3 upload failed for user {UserId} during job apply; falling back to stored ResumeUrl", user.Id);
                    resumeUrl = user.ResumeUrl;
                }
            }
            else if (!string.IsNullOrEmpty(user.ResumeUrl))
            {
                resumeUrl = user.ResumeUrl;
                _logger.LogInformation("Using stored ResumeUrl for user {UserId}: {ResumeUrl}", user.Id, resumeUrl);
            }

            application.ResumeUrl = resumeUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing resume for user {UserId}", user.Id);
            application.ResumeUrl = user.ResumeUrl;
        }

        // Persist the JobApplication entity
        try
        {
            _logger.LogInformation("Adding JobApplication to DbContext for user {UserId}, job {JobId}", user.Id, id);
            _db.JobApplications.Add(application);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Saved JobApplication id {ApplicationId} to database", application.Id);

            // Update Opportunity.Applicants to reflect current unique candidate count
            try
            {
                var dbOpp = await _db.Opportunities.FindAsync(id);
                if (dbOpp != null)
                {
                    dbOpp.Applicants = await _applicantCountService.GetUniqueApplicantCountAsync(id);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Updated Applicants for Opportunity {JobId} to {Count} (unique candidates)", id, dbOpp.Applicants);
                }
            }
            catch (Exception exInner)
            {
                _logger.LogWarning(exInner, "Failed to update Opportunity.Applicants in DB for Job {JobId}", id);
            }

            // STEP 6: Create Job Application Submitted notification (candidate)
            try
            {
                await _notification_service.CreateNotificationAsync(
                    userId: user.Id,
                    title: "Application Submitted",
                    message: "Your application has been submitted successfully.",
                    type: "Success",
                    link: Url.Action("Dashboard", "Candidate")
                );
                _logger.LogInformation("Application submission notification created for user {UserId}, job {JobId}", user.Id, id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create application submission notification for user {UserId}", user.Id);
                // Don't fail the request if notification creation fails
            }

            // PHASE 8 – STEP 7: Employer alert - New Job Application Received
            try
            {
                // Attempt to find the employer (profile) associated with this opportunity
                var employerProfile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.CompanyName == (item.Company ?? string.Empty));
                if (employerProfile != null && !string.IsNullOrWhiteSpace(employerProfile.UserId))
                {
                    await _notification_service.CreateNotificationAsync(
                        userId: employerProfile.UserId,
                        title: "New Job Application",
                        message: "A new candidate has applied for your job posting.",
                        type: "Info",
                        link: Url.Action("Applicants", "Employer", new { id })
                    );
                    _logger.LogInformation("Employer notification created for user {EmployerUserId} about new application for job {JobId}", employerProfile.UserId, id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create employer notification for new application for job {JobId}", id);
                // Non-fatal: do not fail application flow on notification error
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save JobApplication for user {UserId} and job {JobId}", user.Id, id);
            TempData["Error"] = "An error occurred while sending your application. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Update repository in-memory counter for UI parity (safe to call; keeps previous UX)
        try
        {
            _repository.AddApplication(id, model.Application);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update in-memory repository/application list for job {JobId}", id);
        }

        TempData["Success"] = $"Application sent to {item.Company}! We'll keep our fingers crossed.";
        _logger.LogInformation("Apply completed for JobId={JobId}, ApplicationId={ApplicationId}", id, application.Id);
        return RedirectToAction(nameof(Details), new { id });
    }
}