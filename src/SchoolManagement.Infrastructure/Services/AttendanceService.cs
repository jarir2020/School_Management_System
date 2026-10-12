using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Services;

/// <summary>Enforces assignment, review, and family-link rules for attendance records.</summary>
public sealed class AttendanceService(
    IApplicationDbContextFactory contextFactory,
    UserManager<ApplicationUser> userManager) : IAttendanceService
{
    private static readonly string[] AttendanceManagers = [SchoolRoles.SuperAdmin, SchoolRoles.Principal];

    public async Task<AttendanceSetupOptions> GetSetupOptionsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var users = await userManager.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync(cancellationToken);
        var teachers = new List<ApplicationUser>();
        foreach (var user in users)
        {
            if (await userManager.IsInRoleAsync(user, SchoolRoles.Teacher))
            {
                teachers.Add(user);
            }
        }

        var teacherIds = teachers.Select(user => user.Id).ToArray();
        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(profile => teacherIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);
        var teacherOptions = teachers.Select(user => new TeacherOption(
                user.Id,
                profiles.TryGetValue(user.Id, out var profile) && !string.IsNullOrWhiteSpace(profile.FullName)
                    ? profile.FullName
                    : user.Email ?? "Teacher",
                user.Email ?? ""))
            .ToArray();
        var subjects = await db.Subjects.AsNoTracking()
            .OrderBy(subject => subject.Name)
            .Select(subject => new SubjectSummary(subject.Id, subject.Name, subject.Code, subject.FullMarks))
            .ToListAsync(cancellationToken);
        var years = await db.AcademicYears.AsNoTracking()
            .OrderByDescending(year => year.StartDate)
            .Select(year => new AttendanceYearOption(year.Id, year.Name, year.StartDate, year.EndDate))
            .ToListAsync(cancellationToken);
        var sections = await db.Sections.AsNoTracking()
            .Include(section => section.Class)
            .OrderBy(section => section.Class!.GradeLevel)
            .ThenBy(section => section.Name)
            .Select(section => new AttendanceSectionOption(
                section.Id,
                section.Name,
                section.Class!.Name))
            .ToListAsync(cancellationToken);

        return new AttendanceSetupOptions(teacherOptions, subjects, years, sections);
    }

    public async Task<IReadOnlyList<SubjectSummary>> GetSubjectsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subjects.AsNoTracking()
            .OrderBy(subject => subject.Name)
            .Select(subject => new SubjectSummary(subject.Id, subject.Name, subject.Code, subject.FullMarks))
            .ToListAsync(cancellationToken);
    }

    public async Task CreateSubjectAsync(
        string actorUserId,
        SubjectInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        var name = RequiredText(input.Name, "Subject name", 100);
        var code = RequiredText(input.Code, "Subject code", 24).ToUpperInvariant();
        if (input.FullMarks is < 1 or > 10000)
        {
            throw new ArgumentException("Full marks must be between 1 and 10,000.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Subjects.AnyAsync(subject => subject.Code == code, cancellationToken))
        {
            throw new InvalidOperationException("A subject with this code already exists.");
        }

        db.Subjects.Add(new Subject { Name = name, Code = code, FullMarks = input.FullMarks });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeacherAssignmentSummary>> GetAssignmentsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireUserAsync(actorUserId, cancellationToken);
        var isTeacher = await userManager.IsInRoleAsync(actor, SchoolRoles.Teacher);
        var isManager = await HasAnyRoleAsync(actor, AttendanceManagers);
        if (!isTeacher && !isManager)
        {
            throw new UnauthorizedAccessException("You cannot view teacher assignments.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.TeacherAssignments.AsNoTracking().AsQueryable();
        if (isTeacher && !isManager)
        {
            query = query.Where(assignment => assignment.TeacherUserId == actorUserId);
        }

        return await MapAssignmentsAsync(db, query, cancellationToken);
    }

    public async Task CreateAssignmentAsync(
        string actorUserId,
        TeacherAssignmentInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        var teacher = await RequireUserAsync(input.TeacherUserId, cancellationToken);
        if (!await userManager.IsInRoleAsync(teacher, SchoolRoles.Teacher))
        {
            throw new ArgumentException("Select an account with the Teacher role.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var year = await db.AcademicYears.SingleOrDefaultAsync(item => item.Id == input.AcademicYearId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected academic year does not exist.");
        if (!await db.Sections.AnyAsync(section => section.Id == input.SectionId, cancellationToken))
        {
            throw new KeyNotFoundException("The selected section does not exist.");
        }

        if (!await db.Subjects.AnyAsync(subject => subject.Id == input.SubjectId, cancellationToken))
        {
            throw new KeyNotFoundException("The selected subject does not exist.");
        }

        if (await db.TeacherAssignments.AnyAsync(assignment =>
                assignment.TeacherUserId == input.TeacherUserId
                && assignment.SectionId == input.SectionId
                && assignment.SubjectId == input.SubjectId
                && assignment.AcademicYearId == input.AcademicYearId,
                cancellationToken))
        {
            throw new InvalidOperationException("This teacher already has that subject assignment for the selected section and year.");
        }

        db.TeacherAssignments.Add(new TeacherAssignment
        {
            TeacherUserId = teacher.Id,
            SectionId = input.SectionId,
            SubjectId = input.SubjectId,
            AcademicYearId = year.Id,
            AssignedByUserId = actorUserId,
            AssignedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAssignmentAsync(
        string actorUserId,
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.TeacherAssignments.SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken)
            ?? throw new KeyNotFoundException("The teacher assignment no longer exists.");
        if (await db.AttendanceSessions.AnyAsync(session =>
                session.TeacherUserId == assignment.TeacherUserId
                && session.SectionId == assignment.SectionId
                && session.SubjectId == assignment.SubjectId
                && session.AcademicYearId == assignment.AcademicYearId,
                cancellationToken))
        {
            throw new InvalidOperationException("An assignment with attendance history cannot be removed.");
        }

        if (await db.ExamSubjects.AnyAsync(item => item.SectionId == assignment.SectionId
                && item.SubjectId == assignment.SubjectId
                && item.Examination!.AcademicYearId == assignment.AcademicYearId,
                cancellationToken))
        {
            throw new InvalidOperationException("An assignment configured for an examination cannot be removed.");
        }

        db.TeacherAssignments.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceRegister> GetRegisterAsync(
        string teacherUserId,
        int assignmentId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await GetTeacherAssignmentAsync(db, teacherUserId, assignmentId, date, cancellationToken);
        var session = await db.AttendanceSessions.AsNoTracking()
            .Include(item => item.Records).ThenInclude(record => record.Student)
            .SingleOrDefaultAsync(item => item.SectionId == assignment.SectionId
                && item.SubjectId == assignment.SubjectId
                && item.AcademicYearId == assignment.AcademicYearId
                && item.AttendanceDate == date,
                cancellationToken);

        if (session is not null)
        {
            var saved = session.Records.OrderBy(record => record.Student!.FullName)
                .Select(record => new AttendanceRosterEntry(
                    record.StudentId,
                    record.Id,
                    record.Student?.StudentNumber ?? "",
                    record.Student?.FullName ?? "Student",
                    record.Status,
                    record.Note))
                .ToArray();
            return new AttendanceRegister(assignment, date, session.Id, session.EnteredAtUtc, saved);
        }

        var roster = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.SectionId == assignment.SectionId
                && enrollment.AcademicYearId == assignment.AcademicYearId
                && enrollment.Status == EnrollmentStatus.Active
                && enrollment.Student!.Status == StudentStatus.Active)
            .OrderBy(enrollment => enrollment.Student!.FullName)
            .Select(enrollment => new AttendanceRosterEntry(
                enrollment.StudentId,
                null,
                enrollment.Student!.StudentNumber,
                enrollment.Student.FullName,
                null,
                ""))
            .ToListAsync(cancellationToken);
        return new AttendanceRegister(assignment, date, null, null, roster);
    }

    public async Task SubmitAttendanceAsync(
        string teacherUserId,
        int assignmentId,
        DateOnly date,
        IReadOnlyList<AttendanceEntryInput> entries,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("There are no active enrolled students in this section.");
        }

        if (entries.Any(entry => !Enum.IsDefined(entry.Status)))
        {
            throw new ArgumentException("Choose a valid attendance status for every student.");
        }

        if (entries.Any(entry => (entry.Note?.Trim().Length ?? 0) > 300))
        {
            throw new ArgumentException("Each attendance note must be 300 characters or fewer.");
        }

        if (entries.Select(entry => entry.StudentId).Distinct().Count() != entries.Count)
        {
            throw new ArgumentException("A student appears more than once in this attendance submission.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var assignment = await GetTeacherAssignmentAsync(db, teacherUserId, assignmentId, date, cancellationToken);
        if (await db.AttendanceSessions.AnyAsync(session => session.SectionId == assignment.SectionId
                && session.SubjectId == assignment.SubjectId
                && session.AcademicYearId == assignment.AcademicYearId
                && session.AttendanceDate == date,
                cancellationToken))
        {
            throw new InvalidOperationException("Attendance has already been submitted. Request a correction for any mistake.");
        }

        var expectedStudentIds = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.SectionId == assignment.SectionId
                && enrollment.AcademicYearId == assignment.AcademicYearId
                && enrollment.Status == EnrollmentStatus.Active
                && enrollment.Student!.Status == StudentStatus.Active)
            .Select(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);
        if (expectedStudentIds.Count != entries.Count || !expectedStudentIds.ToHashSet().SetEquals(entries.Select(entry => entry.StudentId)))
        {
            throw new InvalidOperationException("The active roster changed. Reload the register before submitting attendance.");
        }

        var session = new AttendanceSession
        {
            SectionId = assignment.SectionId,
            SubjectId = assignment.SubjectId,
            AcademicYearId = assignment.AcademicYearId,
            AttendanceDate = date,
            TeacherUserId = teacherUserId,
            EnteredAtUtc = DateTimeOffset.UtcNow,
            Records = entries.Select(entry => new AttendanceRecord
            {
                StudentId = entry.StudentId,
                Status = entry.Status,
                Note = entry.Note?.Trim() ?? ""
            }).ToList()
        };
        db.AttendanceSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RequestCorrectionAsync(
        string teacherUserId,
        int attendanceRecordId,
        AttendanceStatus requestedStatus,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        if (!Enum.IsDefined(requestedStatus))
        {
            throw new ArgumentException("Choose a valid attendance status.");
        }

        var cleanReason = RequiredText(reason, "Correction reason", 500);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var record = await db.AttendanceRecords
            .Include(item => item.AttendanceSession)
            .SingleOrDefaultAsync(item => item.Id == attendanceRecordId, cancellationToken)
            ?? throw new KeyNotFoundException("The attendance record does not exist.");
        if (record.AttendanceSession?.TeacherUserId != teacherUserId)
        {
            throw new UnauthorizedAccessException("You can request changes only to attendance you entered.");
        }

        if (record.Status == requestedStatus)
        {
            throw new InvalidOperationException("The requested status already matches the saved attendance.");
        }

        if (await db.AttendanceCorrectionRequests.AnyAsync(request =>
                request.AttendanceRecordId == attendanceRecordId && request.Status == CorrectionRequestStatus.Pending,
                cancellationToken))
        {
            throw new InvalidOperationException("A correction request is already waiting for review.");
        }

        db.AttendanceCorrectionRequests.Add(new AttendanceCorrectionRequest
        {
            AttendanceRecordId = attendanceRecordId,
            PreviousStatus = record.Status,
            RequestedStatus = requestedStatus,
            Reason = cleanReason,
            RequestedByUserId = teacherUserId,
            RequestedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceCorrectionSummary>> GetCorrectionsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireUserAsync(actorUserId, cancellationToken);
        var isTeacher = await userManager.IsInRoleAsync(actor, SchoolRoles.Teacher);
        var isApprover = await HasAnyRoleAsync(actor, AttendanceManagers);
        if (!isTeacher && !isApprover)
        {
            throw new UnauthorizedAccessException("You cannot view attendance correction requests.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AttendanceCorrectionRequests.AsNoTracking()
            .Include(request => request.AttendanceRecord)!
                .ThenInclude(record => record!.Student)
            .Include(request => request.AttendanceRecord)!
                .ThenInclude(record => record!.AttendanceSession)!
                    .ThenInclude(session => session!.Section)!
                        .ThenInclude(section => section!.Class)
            .Include(request => request.AttendanceRecord)!
                .ThenInclude(record => record!.AttendanceSession)!
                    .ThenInclude(session => session!.Subject)
            .AsQueryable();
        if (isTeacher && !isApprover)
        {
            query = query.Where(request => request.AttendanceRecord!.AttendanceSession!.TeacherUserId == actorUserId);
        }

        var requests = await query.OrderBy(request => request.Status)
            .ThenByDescending(request => request.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        var userIds = requests.SelectMany(request => new[] { request.RequestedByUserId, request.ReviewedByUserId })
            .Where(id => id is not null).Cast<string>().Distinct().ToArray();
        var names = await GetUserNamesAsync(db, userIds, cancellationToken);
        return requests.Select(request => new AttendanceCorrectionSummary(
                request.Id,
                request.AttendanceRecordId,
                request.AttendanceRecord?.Student?.FullName ?? "Student",
                request.AttendanceRecord?.Student?.StudentNumber ?? "",
                request.AttendanceRecord?.AttendanceSession?.Section?.Class?.Name ?? "",
                request.AttendanceRecord?.AttendanceSession?.Section?.Name ?? "",
                request.AttendanceRecord?.AttendanceSession?.Subject?.Name ?? "",
                request.AttendanceRecord?.AttendanceSession?.AttendanceDate ?? default,
                request.PreviousStatus,
                request.RequestedStatus,
                request.Reason,
                names.GetValueOrDefault(request.RequestedByUserId, request.RequestedByUserId),
                request.RequestedAtUtc,
                request.Status,
                request.ReviewedByUserId is null ? null : names.GetValueOrDefault(request.ReviewedByUserId, request.ReviewedByUserId),
                request.ReviewedAtUtc,
                request.ReviewerRemarks))
            .ToArray();
    }

    public async Task ReviewCorrectionAsync(
        string reviewerUserId,
        int correctionId,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(reviewerUserId, cancellationToken, AttendanceManagers);
        var cleanRemarks = OptionalText(remarks, 500);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await db.AttendanceCorrectionRequests
            .Include(item => item.AttendanceRecord)
            .SingleOrDefaultAsync(item => item.Id == correctionId, cancellationToken)
            ?? throw new KeyNotFoundException("The correction request does not exist.");
        if (request.Status != CorrectionRequestStatus.Pending)
        {
            throw new InvalidOperationException("This correction request has already been reviewed.");
        }

        if (request.RequestedByUserId == reviewerUserId)
        {
            throw new InvalidOperationException("The requester cannot review their own correction.");
        }

        if (approve)
        {
            var record = request.AttendanceRecord
                ?? throw new InvalidOperationException("The attendance record for this request no longer exists.");
            if (record.Status != request.PreviousStatus)
            {
                throw new InvalidOperationException("The attendance record changed after this request was submitted. The request cannot be applied.");
            }

            record.Status = request.RequestedStatus;
        }

        request.Status = approve ? CorrectionRequestStatus.Approved : CorrectionRequestStatus.Rejected;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedAtUtc = DateTimeOffset.UtcNow;
        request.ReviewerRemarks = cleanRemarks;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyAttendanceSession>> GetDailyReportAsync(
        string actorUserId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireUserAsync(actorUserId, cancellationToken);
        var isTeacher = await userManager.IsInRoleAsync(actor, SchoolRoles.Teacher);
        var isManager = await HasAnyRoleAsync(actor, AttendanceManagers);
        if (!isTeacher && !isManager)
        {
            throw new UnauthorizedAccessException("You cannot view attendance reports.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AttendanceSessions.AsNoTracking()
            .Where(session => session.AttendanceDate == date)
            .Include(session => session.Records)
            .Include(session => session.Section)!.ThenInclude(section => section!.Class)
            .Include(session => session.Subject)
            .AsQueryable();
        if (isTeacher && !isManager)
        {
            query = query.Where(session => session.TeacherUserId == actorUserId);
        }

        var sessions = await query.OrderBy(session => session.Section!.Class!.GradeLevel)
            .ThenBy(session => session.Section!.Name)
            .ThenBy(session => session.Subject!.Name)
            .ToListAsync(cancellationToken);
        var names = await GetUserNamesAsync(db, sessions.Select(session => session.TeacherUserId).Distinct().ToArray(), cancellationToken);
        return sessions.Select(session => new DailyAttendanceSession(
                session.Id,
                session.Section?.Class?.Name ?? "",
                session.Section?.Name ?? "",
                session.Subject?.Name ?? "",
                names.GetValueOrDefault(session.TeacherUserId, session.TeacherUserId),
                session.AttendanceDate,
                session.EnteredAtUtc,
                session.Records.Count,
                session.Records.Count(record => record.Status == AttendanceStatus.Present),
                session.Records.Count(record => record.Status == AttendanceStatus.Absent),
                session.Records.Count(record => record.Status == AttendanceStatus.Late),
                session.Records.Count(record => record.Status == AttendanceStatus.Excused)))
            .ToArray();
    }

    public async Task<IReadOnlyList<MonthlyStudentAttendance>> GetMonthlyReportAsync(
        string actorUserId,
        int year,
        int month,
        int? assignmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Choose a valid month and year.");
        }

        var actor = await RequireUserAsync(actorUserId, cancellationToken);
        var isTeacher = await userManager.IsInRoleAsync(actor, SchoolRoles.Teacher);
        var isManager = await HasAnyRoleAsync(actor, AttendanceManagers);
        if (!isTeacher && !isManager)
        {
            throw new UnauthorizedAccessException("You cannot view attendance reports.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AttendanceSessions.AsNoTracking()
            .Where(session => session.AttendanceDate >= new DateOnly(year, month, 1)
                && session.AttendanceDate <= new DateOnly(year, month, DateTime.DaysInMonth(year, month)))
            .Include(session => session.Records).ThenInclude(record => record.Student)
            .Include(session => session.Section)!.ThenInclude(section => section!.Class)
            .Include(session => session.Subject)
            .AsQueryable();
        if (isTeacher && !isManager)
        {
            query = query.Where(session => session.TeacherUserId == actorUserId);
        }

        if (assignmentId.HasValue)
        {
            var assignment = await db.TeacherAssignments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == assignmentId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("The selected assignment does not exist.");
            if (isTeacher && !isManager && assignment.TeacherUserId != actorUserId)
            {
                throw new UnauthorizedAccessException("You can report only on your own assignments.");
            }

            query = query.Where(session => session.SectionId == assignment.SectionId
                && session.SubjectId == assignment.SubjectId
                && session.AcademicYearId == assignment.AcademicYearId);
        }

        var sessions = await query.ToListAsync(cancellationToken);
        return sessions.SelectMany(session => session.Records.Select(record => new
            {
                Session = session,
                Record = record
            }))
            .GroupBy(item => new
            {
                item.Record.StudentId,
                StudentNumber = item.Record.Student?.StudentNumber ?? "",
                StudentName = item.Record.Student?.FullName ?? "Student",
                ClassName = item.Session.Section?.Class?.Name ?? "",
                SectionName = item.Session.Section?.Name ?? "",
                SubjectName = item.Session.Subject?.Name ?? ""
            })
            .Select(group => new MonthlyStudentAttendance(
                group.Key.StudentId,
                group.Key.StudentNumber,
                group.Key.StudentName,
                group.Key.ClassName,
                group.Key.SectionName,
                group.Key.SubjectName,
                group.Count(),
                group.Count(item => item.Record.Status == AttendanceStatus.Present),
                group.Count(item => item.Record.Status == AttendanceStatus.Absent),
                group.Count(item => item.Record.Status == AttendanceStatus.Late),
                group.Count(item => item.Record.Status == AttendanceStatus.Excused)))
            .OrderBy(item => item.ClassName)
            .ThenBy(item => item.SectionName)
            .ThenBy(item => item.StudentName)
            .ThenBy(item => item.SubjectName)
            .ToArray();
    }

    public async Task<IReadOnlyList<GuardianLinkSummary>> GetGuardianLinksAsync(
        string actorUserId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Students.AnyAsync(student => student.Id == studentId, cancellationToken))
        {
            throw new KeyNotFoundException("The student record does not exist.");
        }

        var rows = await db.GuardianAccountLinks.AsNoTracking()
            .Where(account => db.StudentGuardians.Any(link => link.StudentId == studentId && link.GuardianId == account.GuardianId))
            .Include(account => account.Guardian)
            .OrderBy(account => account.Guardian!.FullName)
            .ToListAsync(cancellationToken);
        var ids = rows.SelectMany(row => new[] { row.UserId, row.LinkedByUserId }).Distinct().ToArray();
        var names = await GetUserNamesAsync(db, ids, cancellationToken);
        var emails = await userManager.Users.AsNoTracking().Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? "", cancellationToken);
        return rows.Select(row => new GuardianLinkSummary(
                row.GuardianId,
                row.Guardian?.FullName ?? "Guardian",
                row.Guardian?.Relationship ?? "",
                row.Guardian?.Phone ?? "",
                row.UserId,
                names.GetValueOrDefault(row.UserId, row.UserId),
                emails.GetValueOrDefault(row.UserId, ""),
                row.LinkedAtUtc))
            .ToArray();
    }

    public async Task LinkGuardianAccountAsync(
        string actorUserId,
        int guardianId,
        string parentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        var parent = await RequireUserAsync(parentUserId, cancellationToken);
        if (!await userManager.IsInRoleAsync(parent, SchoolRoles.ParentStudent))
        {
            throw new ArgumentException("Select an account with the Parent/Student role.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Guardians.AnyAsync(guardian => guardian.Id == guardianId, cancellationToken))
        {
            throw new KeyNotFoundException("The guardian record does not exist.");
        }

        if (await db.GuardianAccountLinks.AnyAsync(link => link.GuardianId == guardianId && link.UserId == parentUserId, cancellationToken))
        {
            throw new InvalidOperationException("This parent account is already linked to that guardian.");
        }

        db.GuardianAccountLinks.Add(new GuardianAccountLink
        {
            GuardianId = guardianId,
            UserId = parentUserId,
            LinkedByUserId = actorUserId,
            LinkedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlinkGuardianAccountAsync(
        string actorUserId,
        int guardianId,
        string parentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var link = await db.GuardianAccountLinks.SingleOrDefaultAsync(
            item => item.GuardianId == guardianId && item.UserId == parentUserId,
            cancellationToken);
        if (link is null)
        {
            throw new KeyNotFoundException("The parent account link does not exist.");
        }

        db.GuardianAccountLinks.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParentAttendanceSummary>> GetMyAttendanceAsync(
        string parentUserId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(parentUserId, cancellationToken, SchoolRoles.ParentStudent);
        if (endDate < startDate || endDate.DayNumber - startDate.DayNumber > 366)
        {
            throw new ArgumentException("Choose a date range of up to one year.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(record => record.AttendanceSession!.AttendanceDate >= startDate
                && record.AttendanceSession.AttendanceDate <= endDate
                && db.StudentGuardians.Any(link => link.StudentId == record.StudentId
                    && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == parentUserId)))
            .Include(record => record.Student)
            .Include(record => record.AttendanceSession)!
                .ThenInclude(session => session!.Section)!
                    .ThenInclude(section => section!.Class)
            .Include(record => record.AttendanceSession)!
                .ThenInclude(session => session!.Subject)
            .OrderByDescending(record => record.AttendanceSession!.AttendanceDate)
            .ThenBy(record => record.Student!.FullName)
            .ToListAsync(cancellationToken);
        return rows.Select(record => new ParentAttendanceSummary(
                record.StudentId,
                record.Student?.StudentNumber ?? "",
                record.Student?.FullName ?? "Student",
                record.AttendanceSession?.Section?.Class?.Name ?? "",
                record.AttendanceSession?.Section?.Name ?? "",
                record.AttendanceSession?.Subject?.Name ?? "",
                record.AttendanceSession?.AttendanceDate ?? default,
                record.Status,
                record.Note))
            .ToArray();
    }

    private async Task<TeacherAssignmentSummary> GetTeacherAssignmentAsync(
        IApplicationDbContext db,
        string teacherUserId,
        int assignmentId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var query = db.TeacherAssignments.AsNoTracking()
            .Where(assignment => assignment.Id == assignmentId && assignment.TeacherUserId == teacherUserId);
        var rows = await MapAssignmentsAsync(db, query, cancellationToken);
        var assignment = rows.SingleOrDefault()
            ?? throw new UnauthorizedAccessException("This class and subject are not assigned to your account.");
        if (date < assignment.AcademicYearStart || date > assignment.AcademicYearEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(date), "Attendance date must be within the assigned academic year.");
        }

        return assignment;
    }

    private async Task<IReadOnlyList<TeacherAssignmentSummary>> MapAssignmentsAsync(
        IApplicationDbContext db,
        IQueryable<TeacherAssignment> query,
        CancellationToken cancellationToken)
    {
        var assignments = await query
            .Include(item => item.Section)!.ThenInclude(section => section!.Class)
            .Include(item => item.Subject)
            .Include(item => item.AcademicYear)
            .OrderByDescending(item => item.AcademicYear!.StartDate)
            .ThenBy(item => item.Section!.Class!.GradeLevel)
            .ThenBy(item => item.Section!.Name)
            .ToListAsync(cancellationToken);
        var teacherIds = assignments.Select(item => item.TeacherUserId).Distinct().ToArray();
        var names = await GetUserNamesAsync(db, teacherIds, cancellationToken);
        var emails = await userManager.Users.AsNoTracking().Where(user => teacherIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? "", cancellationToken);
        return assignments.Select(item => new TeacherAssignmentSummary(
                item.Id,
                item.TeacherUserId,
                names.GetValueOrDefault(item.TeacherUserId, emails.GetValueOrDefault(item.TeacherUserId, "Teacher")),
                emails.GetValueOrDefault(item.TeacherUserId, ""),
                item.SectionId,
                item.Section?.Name ?? "",
                item.Section?.Class?.Name ?? "",
                item.SubjectId,
                item.Subject?.Name ?? "",
                item.Subject?.Code ?? "",
                item.AcademicYearId,
                item.AcademicYear?.Name ?? "",
                item.AcademicYear?.StartDate ?? default,
                item.AcademicYear?.EndDate ?? default))
            .ToArray();
    }

    private async Task<Dictionary<string, string>> GetUserNamesAsync(
        IApplicationDbContext db,
        string[] userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.FullName, cancellationToken);
        var emails = await userManager.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.Id, cancellationToken);
        return userIds.Distinct().ToDictionary(
            id => id,
            id => profiles.GetValueOrDefault(id, emails.GetValueOrDefault(id, id)));
    }

    private async Task<ApplicationUser> RequireUserAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("A signed-in school account is required.");
        }

        return await userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
    }

    private async Task EnsureRoleAsync(string userId, CancellationToken cancellationToken, params string[] allowedRoles)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        if (!await HasAnyRoleAsync(user, allowedRoles))
        {
            throw new UnauthorizedAccessException("Your school account is not allowed to perform this action.");
        }
    }

    private async Task<bool> HasAnyRoleAsync(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        foreach (var role in roles)
        {
            if (await userManager.IsInRoleAsync(user, role))
            {
                return true;
            }
        }

        return false;
    }

    private static string RequiredText(string? value, string label, int maximumLength)
    {
        var result = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(result) || result.Length > maximumLength)
        {
            throw new ArgumentException($"{label} is required and must be {maximumLength} characters or fewer.");
        }

        return result;
    }

    private static string? OptionalText(string? value, int maximumLength)
    {
        var result = value?.Trim();
        if (result?.Length > maximumLength)
        {
            throw new ArgumentException($"Text must be {maximumLength} characters or fewer.");
        }

        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
