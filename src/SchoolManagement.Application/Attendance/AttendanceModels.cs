using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Attendance;

public sealed record SubjectSummary(int Id, string Name, string Code, int FullMarks);

public sealed record TeacherOption(string UserId, string FullName, string Email);

public sealed record TeacherAssignmentSummary(
    int Id,
    string TeacherUserId,
    string TeacherName,
    string TeacherEmail,
    int SectionId,
    string SectionName,
    string ClassName,
    int SubjectId,
    string SubjectName,
    string SubjectCode,
    int AcademicYearId,
    string AcademicYearName,
    DateOnly AcademicYearStart,
    DateOnly AcademicYearEnd);

public sealed record AttendanceSetupOptions(
    IReadOnlyList<TeacherOption> Teachers,
    IReadOnlyList<SubjectSummary> Subjects,
    IReadOnlyList<AttendanceYearOption> AcademicYears,
    IReadOnlyList<AttendanceSectionOption> Sections);

public sealed record AttendanceYearOption(int Id, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record AttendanceSectionOption(int Id, string Name, string ClassName);

public sealed record SubjectInput(string Name, string Code, int FullMarks);

public sealed record TeacherAssignmentInput(string TeacherUserId, int SectionId, int SubjectId, int AcademicYearId);

public sealed record AttendanceRosterEntry(
    int StudentId,
    int? RecordId,
    string StudentNumber,
    string StudentName,
    AttendanceStatus? Status,
    string Note);

public sealed record AttendanceRegister(
    TeacherAssignmentSummary Assignment,
    DateOnly AttendanceDate,
    int? SessionId,
    DateTimeOffset? EnteredAtUtc,
    IReadOnlyList<AttendanceRosterEntry> Students)
{
    public bool IsSubmitted => SessionId.HasValue;
}

public sealed record AttendanceEntryInput(int StudentId, AttendanceStatus Status, string? Note);

public sealed record DailyAttendanceSession(
    int SessionId,
    string ClassName,
    string SectionName,
    string SubjectName,
    string TeacherName,
    DateOnly AttendanceDate,
    DateTimeOffset EnteredAtUtc,
    int StudentCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount);

public sealed record MonthlyStudentAttendance(
    int StudentId,
    string StudentNumber,
    string StudentName,
    string ClassName,
    string SectionName,
    string SubjectName,
    int SessionCount,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount)
{
    public decimal AttendancePercentage => SessionCount == 0
        ? 0
        : Math.Round((PresentCount + LateCount + ExcusedCount) * 100m / SessionCount, 1);
}

public sealed record AttendanceCorrectionSummary(
    int Id,
    int RecordId,
    string StudentName,
    string StudentNumber,
    string ClassName,
    string SectionName,
    string SubjectName,
    DateOnly AttendanceDate,
    AttendanceStatus PreviousStatus,
    AttendanceStatus RequestedStatus,
    string Reason,
    string RequestedByName,
    DateTimeOffset RequestedAtUtc,
    CorrectionRequestStatus Status,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewerRemarks);

public sealed record GuardianLinkSummary(
    int GuardianId,
    string GuardianName,
    string Relationship,
    string Phone,
    string UserId,
    string UserName,
    string Email,
    DateTimeOffset LinkedAtUtc);

public sealed record ParentAttendanceSummary(
    int StudentId,
    string StudentNumber,
    string StudentName,
    string ClassName,
    string SectionName,
    string SubjectName,
    DateOnly AttendanceDate,
    AttendanceStatus Status,
    string Note);
