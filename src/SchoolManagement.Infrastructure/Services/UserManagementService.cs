using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.UserManagement;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services;

public sealed class UserManagementService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : IUserManagementService
{
    public async Task<IReadOnlyList<string>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
    {
        return await roleManager.Roles.AsNoTracking()
            .Where(role => role.Name != null)
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedSchoolUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users.AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var ids = users.Select(user => user.Id).ToArray();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(profile => ids.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);

        var results = new List<ManagedSchoolUser>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var isLockedOut = user.LockoutEnabled
                && user.LockoutEnd.HasValue
                && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            profiles.TryGetValue(user.Id, out var profile);
            results.Add(new ManagedSchoolUser(
                user.Id,
                user.Email ?? "",
                profile?.FullName ?? "",
                profile?.Phone ?? "",
                roles.Order(StringComparer.Ordinal).ToArray(),
                isLockedOut));
        }

        return results;
    }

    public async Task CreateUserAsync(CreateSchoolUserInput input, CancellationToken cancellationToken = default)
    {
        var fullName = RequiredText(input.FullName, "Full name", 160);
        var email = RequiredText(input.Email, "Email", 254);
        var phone = input.Phone?.Trim() ?? "";
        if (phone.Length > 40)
        {
            throw new ArgumentException("Phone number must be 40 characters or fewer.");
        }

        var roleNames = await ValidateRolesAsync(input.Roles, cancellationToken);
        if (string.IsNullOrWhiteSpace(input.Password))
        {
            throw new ArgumentException("A temporary password is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user, input.Password);
            EnsureSucceeded(createResult, "create the account");

            var roleResult = await userManager.AddToRolesAsync(user, roleNames);
            EnsureSucceeded(roleResult, "assign the selected roles");

            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                FullName = fullName,
                Phone = phone
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task SetUserLockedOutAsync(
        string userId,
        bool lockedOut,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (lockedOut && userId == currentUserId)
        {
            throw new InvalidOperationException("You cannot lock your own account.");
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("The selected user no longer exists.");

        if (lockedOut && await userManager.IsInRoleAsync(user, SchoolRoles.SuperAdmin))
        {
            await EnsureAnotherActiveSuperAdminExistsAsync(userId, cancellationToken);
        }

        var enableResult = await userManager.SetLockoutEnabledAsync(user, true);
        EnsureSucceeded(enableResult, "enable account lockout");

        DateTimeOffset? lockoutEnd = lockedOut ? DateTimeOffset.UtcNow.AddYears(100) : null;
        var lockResult = await userManager.SetLockoutEndDateAsync(user, lockoutEnd);
        EnsureSucceeded(lockResult, lockedOut ? "lock the account" : "unlock the account");
    }

    public async Task UpdateUserRolesAsync(
        string userId,
        IReadOnlyList<string> roleNames,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("The selected user no longer exists.");
        var requestedRoles = await ValidateRolesAsync(roleNames, cancellationToken);
        var currentRoles = await userManager.GetRolesAsync(user);

        if (userId == currentUserId
            && currentRoles.Contains(SchoolRoles.SuperAdmin, StringComparer.Ordinal)
            && !requestedRoles.Contains(SchoolRoles.SuperAdmin, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("You cannot remove your own Super Admin role.");
        }

        if (currentRoles.Contains(SchoolRoles.SuperAdmin, StringComparer.Ordinal)
            && !requestedRoles.Contains(SchoolRoles.SuperAdmin, StringComparer.Ordinal))
        {
            await EnsureAnotherActiveSuperAdminExistsAsync(userId, cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var removeResult = await userManager.RemoveFromRolesAsync(
                user,
                currentRoles.Except(requestedRoles, StringComparer.Ordinal));
            EnsureSucceeded(removeResult, "remove old roles");

            var addResult = await userManager.AddToRolesAsync(
                user,
                requestedRoles.Except(currentRoles, StringComparer.Ordinal));
            EnsureSucceeded(addResult, "assign new roles");

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<IReadOnlyList<string>> ValidateRolesAsync(
        IEnumerable<string>? requestedRoles,
        CancellationToken cancellationToken)
    {
        var roleNames = requestedRoles?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (roleNames.Length == 0)
        {
            throw new ArgumentException("Select at least one role.");
        }

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                throw new ArgumentException($"The role '{roleName}' does not exist.");
            }
        }

        return roleNames;
    }

    private async Task EnsureAnotherActiveSuperAdminExistsAsync(string excludedUserId, CancellationToken cancellationToken)
    {
        var admins = await userManager.GetUsersInRoleAsync(SchoolRoles.SuperAdmin);
        foreach (var admin in admins.Where(user => user.Id != excludedUserId))
        {
            var isLocked = admin.LockoutEnabled
                && admin.LockoutEnd.HasValue
                && admin.LockoutEnd.Value > DateTimeOffset.UtcNow;
            if (!isLocked)
            {
                return;
            }
        }

        throw new InvalidOperationException("The school must keep at least one active Super Admin account.");
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }

    private static string RequiredText(string? value, string displayName, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{displayName} is required.");
        }

        if (text.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} must be {maxLength} characters or fewer.");
        }

        return text;
    }
}
