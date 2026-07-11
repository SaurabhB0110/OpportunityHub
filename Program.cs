using OpportunityHub.Models;
using OpportunityHub.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpportunityHub.Data; // <-- Ensure ApplicationDbContext namespace is imported

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT")
          ?? Environment.GetEnvironmentVariable("ASPNETCORE_PORT")
          ?? "5000";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.WebHost.ConfigureKestrel(options =>
{
    if (int.TryParse(port, out var portInt))
    {
        options.ListenAnyIP(portInt);
    }
});

// Logging & DataProtection
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")))
    .SetApplicationName("OpportunityHub");

// Bind AWS options and register S3 service
builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection("AWS"));
builder.Services.AddSingleton<IS3Service, S3Service>();

// Database + Identity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// MVC / Razor Pages
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Services
builder.Services.AddScoped<IOpportunityRepository, EfOpportunityRepository>();
builder.Services.AddScoped<ApplicantCountService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

// Apply migrations and seed identity (development-friendly)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        await IdentitySeed.SeedAsync(services);

        // Data migration: normalize existing Opportunity.Category values to the new canonical list.
        // Map of old -> new category values for common legacy strings
        var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Engineering", "Software Development" },
            { "Design", "UI/UX Design" },
            { "Marketing", "Digital Marketing" },
            { "Data", "Data Science" },
            { "Content", "Content Writing" }
            // Add additional mappings here if you discover more legacy values
        };

        try
        {
            var legacyCategories = categoryMap.Keys.ToList();
            var toUpdate = await db.Opportunities
                                   .Where(o => legacyCategories.Contains(o.Category))
                                   .ToListAsync();

            if (toUpdate.Any())
            {
                foreach (var opp in toUpdate)
                {
                    if (opp.Category != null && categoryMap.TryGetValue(opp.Category, out var newCat))
                        opp.Category = newCat;
                }
                await db.SaveChangesAsync();

                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Normalized {Count} Opportunity.Category values to the new canonical categories.", toUpdate.Count);
            }
        }
        catch (Exception exInner)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(exInner, "Failed to normalize Opportunity categories during startup. This is non-fatal.");
        }

        // One-time cleanup: remove duplicate applications and update applicant counts
        var applicantCountService = services.GetRequiredService<ApplicantCountService>();
        var (duplicatesRemoved, opportunitiesUpdated) = await applicantCountService.CleanupDuplicateApplicationsAsync();
        
        var logger2 = services.GetRequiredService<ILogger<Program>>();
        if (duplicatesRemoved > 0 || opportunitiesUpdated > 0)
        {
            logger2.LogInformation("Cleanup completed: {DuplicatesRemoved} duplicate applications removed, {OpportunitiesUpdated} applicant counts updated.", duplicatesRemoved, opportunitiesUpdated);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error migrating, seeding DB, or cleaning up duplicates.");
    }
}

// HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();