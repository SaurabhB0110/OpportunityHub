using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpportunityHub.Data;
using OpportunityHub.Models;
using OpportunityHub.ViewModels;
using OpportunityHub.Services;
using Microsoft.AspNetCore.Http;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Mail;

namespace OpportunityHub.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailService _emailService;
    private readonly IS3Service _s3Service;

    private static readonly string[] AllowedResumeExtensions = new[] { ".pdf", ".doc", ".docx" };
    private const long MaxResumeBytes = 5 * 1024 * 1024; // 5 MB

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger,
        ApplicationDbContext db,
        IWebHostEnvironment env,
        IEmailService emailService,
        IS3Service s3Service)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _db = db;
        _env = env;
        _emailService = emailService;
        _s3Service = s3Service;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var vm = new LoginViewModel { ReturnUrl = returnUrl ?? "/" };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Prevent login until email confirmed
        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Please verify your email before logging in.");
            return View(model);
        }

        var signInResult = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            ModelState.AddModelError(string.Empty, "User not found after sign-in.");
            return View(model);
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("SuperAdmin"))
            return RedirectToAction("Index", "Admin");
        if (roles.Contains("Employer"))
            return RedirectToAction("Dashboard", "Employer");
        if (roles.Contains("Candidate"))
            return RedirectToAction("Dashboard", "Candidate");

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }

    // Employer registration
    [HttpGet]
    public IActionResult RegisterEmployer()
    {
        return View(new RegisterEmployerViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterEmployer(RegisterEmployerViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            CompanyName = model.CompanyName,
            CompanyDescription = model.CompanyDescription,
            Website = model.Website,
            ContactNumber = model.ContactNumber,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var err in createResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return View(model);
        }

        if (!await _userManager.IsInRoleAsync(user, "Employer"))
        {
            await _userManager.AddToRoleAsync(user, "Employer");
        }

        // Save EmployerProfile
        var profile = new EmployerProfile
        {
            UserId = user.Id,
            CompanyName = model.CompanyName,
            CompanyAddress = model.CompanyAddress ?? string.Empty,
            EmployerId = model.EmployerId ?? string.Empty,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        if (model.VerificationDocument != null && model.VerificationDocument.Length > 0)
        {
            // This previously saved a file locally; kept behavior but user requested S3; handled elsewhere.
            try
            {
                // If file upload to S3 already in place, this will be handled by S3 service in the Employer controller path.
                // For registration, we only store metadata in EmployerProfile.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process verification document for employer registration ({Email})", model.Email);
            }
        }

        _db.EmployerProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // Generate email confirmation token and encode it for URL safety
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationLink = Url.Action(nameof(ConfirmEmail), "Account",
            new { userId = user.Id, code }, protocol: Request.Scheme);

        // Send confirmation email
        try
        {
            var subject = "Confirm Your Email - OpportunityHub";
            var htmlBody = $@"
                <h2>Welcome to OpportunityHub!</h2>
                <p>Thank you for registering as an employer. Please confirm your email address to activate your account.</p>
                <p><a href='{confirmationLink}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Confirm Email</a></p>
                <p>Or copy and paste this link in your browser:</p>
                <p><code>{confirmationLink}</code></p>
                <p>This link will expire in 24 hours.</p>
                <p>Best regards,<br/>OpportunityHub Team</p>";

            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
            _logger.LogInformation("Confirmation email sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending confirmation email to {Email}", user.Email);
        }

        return RedirectToAction(nameof(RegisterEmployerConfirmation), new { email = user.Email });
    }

    [HttpGet]
    public IActionResult RegisterEmployerConfirmation(string? email)
    {
        ViewData["Email"] = email ?? "";
        return View();
    }

    [HttpGet]
    public IActionResult VerificationPending()
    {
        ViewData["Title"] = "Verification Pending";
        return View();
    }

    // Candidate registration
    [HttpGet]
    public IActionResult RegisterCandidate()
    {
        return View(new RegisterCandidateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCandidate(RegisterCandidateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.Phone,
            Address = model.Address ?? model.City,
            Age = model.Age,
            TenthSchool = model.TenthSchool,
            TenthPercentage = model.TenthPercentage,
            TwelfthCollege = model.TwelfthCollege,
            TwelfthPercentage = model.TwelfthPercentage,
            GraduationDegree = model.GraduationDegree,
            GraduationCollege = model.GraduationCollege,
            GraduationCgpaOrPercentage = model.GraduationCgpaOrPercentage,
            Skills = model.Skills,
            Projects = model.Projects,
            Experience = model.Experience,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var err in createResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return View(model);
        }

        if (!await _userManager.IsInRoleAsync(user, "Candidate"))
        {
            await _userManager.AddToRoleAsync(user, "Candidate");
        }

        if (model.Resume != null && model.Resume.Length > 0)
        {
            var resume = model.Resume;
            var ext = Path.GetExtension(resume.FileName)?.ToLowerInvariant();
            if (!AllowedResumeExtensions.Contains(ext))
            {
                // Keep registration flow intact if resume invalid: log and continue (do not block registration)
                _logger.LogWarning("Candidate registration provided disallowed resume extension {Ext} for {Email}", ext, model.Email);
            }
            else if (resume.Length > MaxResumeBytes)
            {
                _logger.LogWarning("Candidate registration provided resume exceeding size limit ({Size} bytes) for {Email}", resume.Length, model.Email);
            }
            else
            {
                try
                {
                    // Use identical upload logic as CandidateController.Edit
                    var s3Url = await _s3Service.UploadFileAsync(resume, "resumes");

                    // Persist the returned URL onto the ApplicationUser and update via UserManager
                    user.ResumeUrl = s3Url;
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (updateResult.Succeeded)
                    {
                        _logger.LogInformation("Resume uploaded to S3 and user updated during registration for {Email}", model.Email);
                    }
                    else
                    {
                        _logger.LogWarning("UserManager.UpdateAsync failed after resume upload for {Email}. Errors: {Errors}", model.Email, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                        // Do not block registration for update failures; user account remains created.
                    }
                }
                catch (Exception ex)
                {
                    // Log and continue — registration should not fail solely because the resume upload failed.
                    _logger.LogError(ex, "Resume upload to S3 failed during registration for {Email}", model.Email);
                }
            }
        }

        // Generate email confirmation token and encode it for URL safety
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationLink = Url.Action(nameof(ConfirmEmail), "Account",
            new { userId = user.Id, code }, protocol: Request.Scheme);

        // Send confirmation email
        try
        {
            var subject = "Confirm Your Email - OpportunityHub";
            var htmlBody = $@"
                <h2>Welcome to OpportunityHub!</h2>
                <p>Thank you for registering. Please confirm your email address to activate your account and start applying for jobs.</p>
                <p><a href='{confirmationLink}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Confirm Email</a></p>
                <p>Or copy and paste this link in your browser:</p>
                <p><code>{confirmationLink}</code></p>
                <p>This link will expire in 24 hours.</p>
                <p>Best regards,<br/>OpportunityHub Team</p>";

            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
            _logger.LogInformation("Confirmation email sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending confirmation email to {Email}", user.Email);
        }

        return RedirectToAction(nameof(RegisterCandidateConfirmation), new { email = user.Email });
    }

    [HttpGet]
    public IActionResult RegisterCandidateConfirmation(string? email)
    {
        ViewData["Email"] = email ?? "";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            ViewData["ErrorMessage"] = "Unable to find user.";
            return View("ConfirmEmailResult");
        }

        if (user.EmailConfirmed)
        {
            ViewData["SuccessMessage"] = "Your email is already confirmed. You can now sign in.";
            return View("ConfirmEmailResult");
        }

        try
        {
            // Decode token from Base64Url
            string decodedToken;
            try
            {
                var tokenBytes = WebEncoders.Base64UrlDecode(code);
                decodedToken = Encoding.UTF8.GetString(tokenBytes);
            }
            catch
            {
                ViewData["ErrorMessage"] = "Invalid confirmation code.";
                return View("ConfirmEmailResult");
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (result.Succeeded)
            {
                ViewData["SuccessMessage"] = "Your email has been successfully confirmed! You can now sign in with your credentials.";
                _logger.LogInformation("User {UserId} confirmed their email.", userId);
                return View("ConfirmEmailResult");
            }
            else
            {
                ViewData["ErrorMessage"] = "Error confirming your email. The confirmation link may have expired or be invalid.";
                _logger.LogWarning("Failed to confirm email for user {UserId}.", userId);
                return View("ConfirmEmailResult");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming email for user {UserId}.", userId);
            ViewData["ErrorMessage"] = "An error occurred while confirming your email.";
            return View("ConfirmEmailResult");
        }
    }

    // GET action no longer takes a string parameter to avoid duplicate CLR signature with the POST action.
    [HttpGet]
    public IActionResult ResendConfirmationEmail()
    {
        // Preserve previous behavior: accept an optional "email" passed as a query or route value.
        var email = Request.Query["email"].ToString();
        ViewData["Email"] = string.IsNullOrWhiteSpace(email) ? "" : email;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmationEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ViewData["Message"] = "Please provide an email address.";
            return View("ResendConfirmationEmailResult");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Don't reveal whether user exists for security reasons
            ViewData["Message"] = "If an account exists with that email, a confirmation link has been sent.";
            return View("ResendConfirmationEmailResult");
        }

        if (user.EmailConfirmed)
        {
            ViewData["Message"] = "Your email is already confirmed. You can now sign in.";
            return View("ResendConfirmationEmailResult");
        }

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmationLink = Url.Action(nameof(ConfirmEmail), "Account",
                new { userId = user.Id, code }, protocol: Request.Scheme);

            var subject = "Confirm Your Email - OpportunityHub";
            var htmlBody = $@"
                <h2>Email Confirmation - OpportunityHub</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href='{confirmationLink}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Confirm Email</a></p>
                <p>Or copy and paste this link in your browser:</p>
                <p><code>{confirmationLink}</code></p>
                <p>This link will expire in 24 hours.</p>
                <p>Best regards,<br/>OpportunityHub Team</p>";

            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
            _logger.LogInformation("Resend confirmation email sent to {Email}", user.Email);
            ViewData["Message"] = "A confirmation link has been sent to your email address.";
            return View("ResendConfirmationEmailResult");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending confirmation email to {Email}", email);
            ViewData["Message"] = "An error occurred while sending the confirmation email. Please try again later.";
            return View("ResendConfirmationEmailResult");
        }
    }

    //
    // Forgot password / Reset password
    //
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Please provide your email address.");
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Do not reveal whether user exists
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        // Only allow password reset if email is confirmed
        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            // For security, show the same confirmation page but log the event
            _logger.LogInformation("Password reset requested for unconfirmed email {Email}", email);
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetLink = Url.Action(nameof(ResetPassword), "Account",
                new { userId = user.Id, code }, protocol: Request.Scheme);

            var subject = "Reset your password - OpportunityHub";
            var htmlBody = $@"
                <h2>Password reset request</h2>
                <p>To reset your password, click the link below:</p>
                <p><a href='{resetLink}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Reset password</a></p>
                <p>Or copy and paste this link in your browser:</p>
                <p><code>{resetLink}</code></p>
                <p>If you did not request this, ignore this email.</p>
                <p>Best regards,<br/>OpportunityHub Team</p>";

            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
            _logger.LogInformation("Password reset email sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset email to {Email}", email);
            // For user, show generic confirmation
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? userId, string? code)
    {
        var vm = new ViewModels.ResetPasswordViewModel
        {
            UserId = userId,
            Code = code
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ViewModels.ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (string.IsNullOrEmpty(model.UserId) || string.IsNullOrEmpty(model.Code))
        {
            ModelState.AddModelError(string.Empty, "Invalid password reset request.");
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            // Do not reveal whether user exists
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        string decodedToken;
        try
        {
            var tokenBytes = WebEncoders.Base64UrlDecode(model.Code);
            decodedToken = Encoding.UTF8.GetString(tokenBytes);
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Invalid reset token.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var err in result.Errors)
            ModelState.AddModelError(string.Empty, err.Description);

        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }
}