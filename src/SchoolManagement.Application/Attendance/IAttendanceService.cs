using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceSetupOptions> GetSetupOptionsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubjectSummary>> GetSubjectsAsync(CancellationToken cancellationToken = default);
    Task CreateSubjectAsync(string actorUserId, SubjectInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeacherAssignmentSummary>> GetAssignmentsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task CreateAssignmentAsync(string actorUserId, TeacherAssignmentInput input, CancellationToken cancellationToken = default);
    Task DeleteAssignmentAsync(string actorUserId, int assignmentId, CancellationToken cancellationToken = default);

    Task<AttendanceRegister> GetRegisterAsync(string teacherUserId, int assignmentId, DateOnly date, CancellationToken cancellationToken = default);
    Task SubmitAttendanceAsync(string teacherUserId, int assignmentId, DateOnly date, IReadOnlyList<AttendanceEntryInput> entries, CancellationToken cancellationToken = default);
    Task RequestCorrectionAsync(string teacherUserId, int attendanceRecordId, AttendanceStatus requestedStatus, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceCorrectionSummary>> GetCorrectionsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task ReviewCorrectionAsync(string reviewerUserId, int correctionId, bool approve, string? remarks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyAttendanceSession>> GetDailyReportAsync(string actorUserId, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlyStudentAttendance>> GetMonthlyReportAsync(string actorUserId, int year, int month, int? assignmentId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuardianLinkSummary>> GetGuardianLinksAsync(string actorUserId, int studentId, CancellationToken cancellationToken = default);
    Task LinkGuardianAccountAsync(string actorUserId, int guardianId, string parentUserId, CancellationToken cancellationToken = default);
    Task UnlinkGuardianAccountAsync(string actorUserId, int guardianId, string parentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParentAttendanceSummary>> GetMyAttendanceAsync(string parentUserId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}
