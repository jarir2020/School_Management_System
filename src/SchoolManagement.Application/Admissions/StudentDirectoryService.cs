using System.Text;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// Searches and maintains student records without deleting their enrollment or status history.
/// </summary>
public sealed class StudentDirectoryService(IApplicationDbContextFactory contextFactory) : IStudentDirectoryService
{
    public async Task<PagedResult<StudentSummary>> SearchAsync(
        StudentSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = ApplyFilters(db.Students.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var students = await query
            .Include(student => student.Guardians).ThenInclude(link => link.Guardian)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.AcademicYear)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.Section)!.ThenInclude(section => section!.Class)
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var rows = students.Select(student => ToSummary(student, filter.AcademicYearId)).ToArray();
        return new PagedResult<StudentSummary>(rows, page, pageSize, total);
    }

    public async Task<StudentDetails?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var student = await db.Students.AsNoTracking()
            .Include(item => item.Guardians).ThenInclude(link => link.Guardian)
            .Include(item => item.Enrollments).ThenInclude(enrollment => enrollment.AcademicYear)
            .Include(item => item.Enrollments).ThenInclude(enrollment => enrollment.Section)!.ThenInclude(section => section!.Class)
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (student is null)
        {
            return null;
        }

        return new StudentDetails(
            student.Id,
            student.StudentNumber,
            student.FullName,
            student.DateOfBirth,
            student.Gender,
            student.Address,
            student.Status,
            student.Guardians
                .Where(link => link.Guardian is not null)
                .Select(link => new StudentGuardianInfo(
                    link.Guardian!.Id,
                    link.Guardian.FullName,
                    link.Guardian.Relationship,
                    link.Guardian.Phone,
                    link.Guardian.Email,
                    link.Guardian.Address))
                .ToArray(),
            student.Enrollments
                .OrderByDescending(enrollment => enrollment.AcademicYear?.StartDate)
                .Select(enrollment => new StudentEnrollmentInfo(
                    enrollment.AcademicYearId,
                    enrollment.AcademicYear?.Name ?? "",
                    enrollment.SectionId,
                    enrollment.Section?.Name ?? "",
                    enrollment.Section?.Class?.Name ?? "",
                    enrollment.Status,
                    enrollment.EnrolledAtUtc))
                .ToArray(),
            student.StatusHistory.OrderByDescending(history => history.ChangedAtUtc)
                .Select(history => new StudentStatusChangeInfo(
                    history.PreviousStatus,
                    history.NewStatus,
                    history.ChangedByUserId,
                    history.Reason,
                    history.ChangedAtUtc))
                .ToArray());
    }

    public async Task UpdateAsync(
        int id,
        StudentUpdateInput input,
        string changedByUserId,
        CancellationToken cancellationToken = default)
    {
        var fullName = RequiredText(input.FullName, "Student name", 160);
        if (input.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Date of birth must be in the past.");
        }

        var gender = OptionalText(input.Gender, "Gender", 40);
        var address = OptionalText(input.Address, "Student address", 500);
        var guardianName = RequiredText(input.GuardianFullName, "Guardian name", 160);
        var relationship = RequiredText(input.GuardianRelationship, "Relationship", 60);
        var phone = RequiredText(input.GuardianPhone, "Guardian phone", 40);
        if (!new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(phone))
        {
            throw new ArgumentException("Enter a valid guardian phone number.");
        }

        var email = OptionalText(input.GuardianEmail, "Guardian email", 254);
        if (!string.IsNullOrWhiteSpace(email)
            && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            throw new ArgumentException("Enter a valid guardian email address.");
        }

        var guardianAddress = OptionalText(input.GuardianAddress, "Guardian address", 500);
        if (!Enum.IsDefined(input.Status))
        {
            throw new ArgumentOutOfRangeException(nameof(input.Status));
        }

        var changedBy = RequiredText(changedByUserId, "User", 450);
        var reason = OptionalText(input.StatusReason, "Status reason", 500);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var student = await db.Students
            .Include(item => item.Guardians).ThenInclude(link => link.Guardian)
            .Include(item => item.Enrollments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The selected student no longer exists.");

        var statusChanged = student.Status != input.Status;
        if (statusChanged
            && (student.Status != StudentStatus.Active || input.Status != StudentStatus.Withdrawn))
        {
            throw new InvalidOperationException("Phase 2 supports withdrawal from an active student record. Transfers and graduation come in later phases.");
        }

        if (student.Status != input.Status && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required when changing a student's status.");
        }

        student.FullName = fullName;
        student.DateOfBirth = input.DateOfBirth;
        student.Gender = gender;
        student.Address = address;

        var guardian = student.Guardians.Select(link => link.Guardian).FirstOrDefault(item => item is not null);
        if (guardian is null)
        {
            guardian = new Guardian();
            db.Guardians.Add(guardian);
            db.StudentGuardians.Add(new StudentGuardian { Student = student, Guardian = guardian });
        }

        guardian.FullName = guardianName;
        guardian.Relationship = relationship;
        guardian.Phone = phone;
        guardian.Email = email;
        guardian.Address = guardianAddress;

        if (statusChanged)
        {
            var previousStatus = student.Status;
            student.Status = input.Status;
            foreach (var enrollment in student.Enrollments.Where(item => item.Status == EnrollmentStatus.Active))
            {
                enrollment.Status = EnrollmentStatus.Withdrawn;
            }

            db.StudentStatusHistories.Add(new StudentStatusHistory
            {
                Student = student,
                PreviousStatus = previousStatus,
                NewStatus = input.Status,
                ChangedByUserId = changedBy,
                Reason = reason,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = changedBy,
            Action = "StudentProfileUpdated",
            EntityType = "Student",
            EntityId = student.Id.ToString(),
            Details = statusChanged ? $"Profile updated; status changed to {input.Status}." : "Student and guardian profile updated.",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudentExportFile> ExportCsvAsync(
        StudentSearchFilter filter,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var students = await ApplyFilters(db.Students.AsNoTracking(), filter)
            .Include(student => student.Guardians).ThenInclude(link => link.Guardian)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.AcademicYear)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.Section)!.ThenInclude(section => section!.Class)
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentNumber)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Student number,Name,Date of birth,Status,Class,Section,Academic year,Guardian,Guardian phone");
        foreach (var student in students)
        {
            var row = ToSummary(student, filter.AcademicYearId);
            csv.Append(Csv(row.StudentNumber)).Append(',')
                .Append(Csv(row.FullName)).Append(',')
                .Append(Csv(row.DateOfBirth.ToString("yyyy-MM-dd"))).Append(',')
                .Append(Csv(row.Status.ToString())).Append(',')
                .Append(Csv(row.ClassName ?? "")).Append(',')
                .Append(Csv(row.SectionName ?? "")).Append(',')
                .Append(Csv(row.AcademicYearName ?? "")).Append(',')
                .Append(Csv(student.Guardians.Select(link => link.Guardian?.FullName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? ""))
                .Append(',').Append(Csv(row.GuardianPhone)).AppendLine();
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = "StudentCsvExport",
            EntityType = "Export",
            EntityId = "",
            Details = $"Exported {students.Count} student record(s).",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new StudentExportFile(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString()),
            "students.csv",
            "text/csv; charset=utf-8");
    }

    private static IQueryable<Student> ApplyFilters(IQueryable<Student> query, StudentSearchFilter filter)
    {
        if (filter.Status.HasValue)
        {
            query = query.Where(student => student.Status == filter.Status.Value);
        }

        if (filter.AcademicYearId.HasValue || filter.SectionId.HasValue)
        {
            query = query.Where(student => student.Enrollments.Any(enrollment =>
                (!filter.AcademicYearId.HasValue || enrollment.AcademicYearId == filter.AcademicYearId.Value)
                && (!filter.SectionId.HasValue || enrollment.SectionId == filter.SectionId.Value)));
        }

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(student => student.StudentNumber.Contains(search)
                || student.FullName.Contains(search)
                || student.Guardians.Any(link => link.Guardian != null && link.Guardian.Phone.Contains(search)));
        }

        return query;
    }

    private static StudentSummary ToSummary(Student student, int? academicYearId)
    {
        var enrollments = student.Enrollments
            .Where(item => !academicYearId.HasValue || item.AcademicYearId == academicYearId.Value)
            .OrderByDescending(item => item.AcademicYear?.StartDate)
            .ThenByDescending(item => item.EnrolledAtUtc)
            .ToList();
        var enrollment = academicYearId.HasValue
            ? enrollments.FirstOrDefault()
            : enrollments.FirstOrDefault(item => item.Status == EnrollmentStatus.Active) ?? enrollments.FirstOrDefault();
        var guardianPhone = student.Guardians
            .Select(link => link.Guardian?.Phone)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

        return new StudentSummary(
            student.Id,
            student.StudentNumber,
            student.FullName,
            student.DateOfBirth,
            student.Status,
            enrollment?.Section?.Name,
            enrollment?.Section?.Class?.Name,
            enrollment?.AcademicYear?.Name,
            guardianPhone);
    }

    private static string Csv(string value)
    {
        // Prefix spreadsheet formula starters so imported names are treated as plain text.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string RequiredText(string? value, string field, int maximumLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{field} is required.");
        }

        if (text.Length > maximumLength)
        {
            throw new ArgumentException($"{field} must be {maximumLength} characters or fewer.");
        }

        return text;
    }

    private static string OptionalText(string? value, string field, int maximumLength)
    {
        var text = value?.Trim() ?? "";
        if (text.Length > maximumLength)
        {
            throw new ArgumentException($"{field} must be {maximumLength} characters or fewer.");
        }

        return text;
    }
}
