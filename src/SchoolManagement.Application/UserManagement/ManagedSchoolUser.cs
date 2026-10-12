namespace SchoolManagement.Application.UserManagement;

public sealed record ManagedSchoolUser(
    string Id,
    string Email,
    string FullName,
    string Phone,
    IReadOnlyList<string> Roles,
    bool IsLockedOut);

public sealed record CreateSchoolUserInput(
    string FullName,
    string Email,
    string Phone,
    string Password,
    IReadOnlyList<string> Roles);
