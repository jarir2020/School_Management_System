using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Seeding;

/// <summary>
/// Creates fixed role records and an optional first administrator from local configuration.
/// </summary>
public sealed class InitialDataSeeder(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<InitialDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in SchoolRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the {roleName} role: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }

        var profile = await dbContext.SchoolProfiles.FindAsync(
            [SchoolProfile.PrimaryProfileId],
            cancellationToken);
        if (profile is null)
        {
            // A blank profile gives the setup screen a row to edit without inventing school details.
            dbContext.SchoolProfiles.Add(new SchoolProfile());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var adminEmail = configuration["SeedAdmin:Email"]?.Trim();
        var adminPassword = configuration["SeedAdmin:Password"];
        var adminName = configuration["SeedAdmin:FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogInformation("No bootstrap administrator is configured. Set SeedAdmin:Email and SeedAdmin:Password in user secrets to create one.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the bootstrap administrator: " +
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(admin, SchoolRoles.SuperAdmin))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, SchoolRoles.SuperAdmin);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not assign the bootstrap administrator role: " +
                    string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        if (!await dbContext.UserProfiles.AnyAsync(item => item.UserId == admin.Id, cancellationToken))
        {
            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = admin.Id,
                FullName = string.IsNullOrWhiteSpace(adminName) ? "School Administrator" : adminName,
                Phone = ""
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
