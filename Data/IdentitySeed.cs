using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpportunityHub.Models;

namespace OpportunityHub.Data;

public static class IdentitySeed
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("IdentitySeed");

        // Ensure required roles exist
        string[] roles = new[] { "SuperAdmin", "Employer", "Candidate" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(r));
                if (roleResult.Succeeded)
                {
                    logger?.LogInformation("Role '{Role}' created.", r);
                }
                else
                {
                    logger?.LogError("Failed to create role '{Role}': {Errors}", r, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger?.LogDebug("Role '{Role}' already exists.", r);
            }
        }

        // Note: Initial SuperAdmin account creation and password-reset helpers were intentionally removed.
        // This seed now only ensures roles exist and logs results.
    }
}