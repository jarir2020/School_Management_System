namespace SchoolManagement.Application.Admissions;

public interface IStudentDirectoryService
{
    Task<PagedResult<StudentSummary>> SearchAsync(StudentSearchFilter filter, CancellationToken cancellationToken = default);

    Task<StudentDetails?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, StudentUpdateInput input, string changedByUserId, CancellationToken cancellationToken = default);

    Task<StudentExportFile> ExportCsvAsync(StudentSearchFilter filter, string actorUserId, CancellationToken cancellationToken = default);
}
