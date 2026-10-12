namespace SchoolManagement.Application.UserManagement;

public interface IUserManagementService
{
    Task<IReadOnlyList<string>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagedSchoolUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task CreateUserAsync(CreateSchoolUserInput input, CancellationToken cancellationToken = default);
    Task SetUserLockedOutAsync(string userId, bool lockedOut, string currentUserId, CancellationToken cancellationToken = default);
    Task UpdateUserRolesAsync(string userId, IReadOnlyList<string> roleNames, string currentUserId, CancellationToken cancellationToken = default);
}
