using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Admissions;

/// <summary>
/// Owns the public application and staff review workflow, including atomic enrollment on approval.
/// </summary>
public sealed class AdmissionService(
    IApplicationDbContextFactory contextFactory,
    IPrivateDocumentStore documentStore) : IAdmissionService
{
    private const int MaximumDocumentBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<AdmissionAcademicYear>> GetAcademicYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AcademicYears.AsNoTracking()
            .OrderByDescending(year => year.StartDate)
            .Select(year => new AdmissionAcademicYear(year.Id, year.Name, year.StartDate, year.EndDate, year.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdmissionSection>> GetSectionsAsync(int academicYearId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var sections = await db.Sections.AsNoTracking()
            .Include(section => section.Class)
            .OrderBy(section => section.Class!.GradeLevel)
            .ThenBy(section => section.Name)
            .ToListAsync(cancellationToken);
        var counts = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.AcademicYearId == academicYearId && enrollment.Status == EnrollmentStatus.Active)
            .GroupBy(enrollment => enrollment.SectionId)
            .Select(group => new { SectionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SectionId, item => item.Count, cancellationToken);

        return sections.Select(section => new AdmissionSection(
                section.Id,
                section.Name,
                section.Class?.Name ?? "",
                section.Capacity,
                counts.GetValueOrDefault(section.Id)))
            .ToArray();
    }

    public async Task<AdmissionReceipt> SubmitAsync(
        AdmissionSubmissionInput input,
        IReadOnlyCollection<AdmissionDocumentUpload> documents,
        CancellationToken cancellationToken = default)
    {
        var details = ValidateSubmission(input);
        var validatedDocuments = ValidateDocuments(documents);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!await db.AcademicYears.AnyAsync(year => year.Id == details.AcademicYearId, cancellationToken))
        {
            throw new KeyNotFoundException("Select an existing academic year.");
        }

        if (details.RequestedSectionId.HasValue)
        {
            var section = await db.Sections.SingleOrDefaultAsync(
                item => item.Id == details.RequestedSectionId.Value,
                cancellationToken);
            if (section is null)
            {
                throw new KeyNotFoundException("The selected section is no longer available.");
            }

            var activeEnrollments = await db.Enrollments.CountAsync(
                enrollment => enrollment.SectionId == section.Id
                    && enrollment.AcademicYearId == details.AcademicYearId
                    && enrollment.Status == EnrollmentStatus.Active,
                cancellationToken);
            if (activeEnrollments >= section.Capacity)
            {
                throw new InvalidOperationException("The selected section is full. Choose another section or leave the preference blank.");
            }
        }

        var storedDocuments = new List<(ValidatedDocument Document, string StorageKey)>();
        try
        {
            foreach (var document in validatedDocuments)
            {
                var storageKey = await documentStore.SaveAsync(document.Upload.Content, document.Extension, cancellationToken);
                storedDocuments.Add((document, storageKey));
            }

            var applicationNumber = await CreateApplicationNumberAsync(db, cancellationToken);
            var application = new AdmissionApplication
            {
                ApplicationNumber = applicationNumber,
                AcademicYearId = details.AcademicYearId,
                RequestedSectionId = details.RequestedSectionId,
                StudentFullName = details.StudentFullName,
                DateOfBirth = details.DateOfBirth,
                Gender = details.Gender,
                StudentAddress = details.StudentAddress,
                GuardianFullName = details.GuardianFullName,
                GuardianRelationship = details.GuardianRelationship,
                GuardianPhone = details.GuardianPhone,
                GuardianEmail = details.GuardianEmail,
                GuardianAddress = details.GuardianAddress,
                SubmittedAtUtc = DateTimeOffset.UtcNow,
                Status = AdmissionStatus.PendingReview
            };

            foreach (var (document, storageKey) in storedDocuments)
            {
                application.Documents.Add(new ApplicationDocument
                {
                    Type = document.Upload.Type,
                    OriginalFileName = document.FileName,
                    ContentType = document.ContentType,
                    SizeBytes = document.Upload.Content.Length,
                    StorageKey = storageKey,
                    UploadedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            db.AdmissionApplications.Add(application);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdmissionReceipt(application.ApplicationNumber);
        }
        catch
        {
            foreach (var (_, storageKey) in storedDocuments)
            {
                try
                {
                    await documentStore.DeleteAsync(storageKey, cancellationToken);
                }
                catch (IOException)
                {
                    // Preserve the database error; an orphaned private file can be removed during maintenance.
                }
            }

            throw;
        }
    }

    public async Task<PagedResult<AdmissionSummary>> SearchApplicationsAsync(
        AdmissionSearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.AdmissionApplications.AsNoTracking().AsQueryable();
        if (filter.Status.HasValue)
        {
            query = query.Where(application => application.Status == filter.Status.Value);
        }

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(application =>
                EF.Functions.Like(application.ApplicationNumber, pattern)
                || EF.Functions.Like(application.StudentFullName, pattern)
                || EF.Functions.Like(application.GuardianFullName, pattern)
                || EF.Functions.Like(application.GuardianPhone, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(application => application.SubmittedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(application => new AdmissionSummary(
                application.Id,
                application.ApplicationNumber,
                application.StudentFullName,
                application.GuardianFullName,
                application.GuardianPhone,
                application.AcademicYear!.Name,
                application.Status,
                application.SubmittedAtUtc,
                application.Documents.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdmissionSummary>(rows, page, pageSize, total);
    }

    public async Task<AdmissionDetails?> GetApplicationAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var application = await db.AdmissionApplications.AsNoTracking()
            .Include(item => item.AcademicYear)
            .Include(item => item.RequestedSection)!.ThenInclude(section => section!.Class)
            .Include(item => item.Documents)
            .Include(item => item.Reviews)
            .Include(item => item.Student)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (application is null)
        {
            return null;
        }

        return new AdmissionDetails(
            application.Id,
            application.ApplicationNumber,
            application.AcademicYearId,
            application.AcademicYear?.Name ?? "",
            application.RequestedSectionId,
            application.RequestedSection is null ? null : $"{application.RequestedSection.Class?.Name} — {application.RequestedSection.Name}",
            application.StudentFullName,
            application.DateOfBirth,
            application.Gender,
            application.StudentAddress,
            application.GuardianFullName,
            application.GuardianRelationship,
            application.GuardianPhone,
            application.GuardianEmail,
            application.GuardianAddress,
            application.Status,
            application.SubmittedAtUtc,
            application.Student?.Id,
            application.Student?.StudentNumber,
            application.Documents.OrderBy(document => document.Type)
                .Select(document => new AdmissionDocumentInfo(
                    document.Id,
                    document.OriginalFileName,
                    document.ContentType,
                    document.SizeBytes,
                    document.Type,
                    document.UploadedAtUtc))
                .ToArray(),
            application.Reviews.OrderByDescending(review => review.ReviewedAtUtc)
                .Select(review => new AdmissionReviewInfo(
                    review.Decision,
                    review.ReviewerUserId,
                    review.Remarks,
                    review.ReviewedAtUtc))
                .ToArray());
    }

    public async Task<AdmissionDecisionResult> DecideAsync(
        int applicationId,
        AdmissionReviewInput input,
        string reviewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (input.Decision is not (AdmissionDecision.Approved or AdmissionDecision.Rejected))
        {
            throw new ArgumentOutOfRangeException(nameof(input.Decision), "Choose approve or reject.");
        }

        var remarks = OptionalText(input.Remarks, "Review remarks", 1000);
        var reviewerId = RequiredText(reviewerUserId, "Reviewer", 450);
        if (input.Decision == AdmissionDecision.Rejected && string.IsNullOrWhiteSpace(remarks))
        {
            throw new ArgumentException("Add a reason when rejecting an application.");
        }

        if (input.Decision == AdmissionDecision.Approved && !input.SectionId.HasValue)
        {
            throw new ArgumentException("Choose a section before approving an application.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var application = await db.AdmissionApplications
                .Include(item => item.AcademicYear)
                .SingleOrDefaultAsync(item => item.Id == applicationId, cancellationToken)
                ?? throw new KeyNotFoundException("The selected application no longer exists.");
            if (application.Status != AdmissionStatus.PendingReview)
            {
                throw new InvalidOperationException("This application has already been reviewed.");
            }

            string? studentNumber = null;
            if (input.Decision == AdmissionDecision.Approved)
            {
                var section = await db.Sections.SingleOrDefaultAsync(
                    item => item.Id == input.SectionId!.Value,
                    cancellationToken)
                    ?? throw new KeyNotFoundException("Choose an existing class section.");
                var activeEnrollments = await db.Enrollments.CountAsync(
                    enrollment => enrollment.SectionId == section.Id
                        && enrollment.AcademicYearId == application.AcademicYearId
                        && enrollment.Status == EnrollmentStatus.Active,
                    cancellationToken);
                if (activeEnrollments >= section.Capacity)
                {
                    throw new InvalidOperationException("This section is full. Choose a section with available capacity.");
                }

                studentNumber = await CreateStudentNumberAsync(
                    db,
                    application.AcademicYear!.StartDate.Year,
                    cancellationToken);
                var student = new Student
                {
                    AdmissionApplicationId = application.Id,
                    StudentNumber = studentNumber,
                    FullName = application.StudentFullName,
                    DateOfBirth = application.DateOfBirth,
                    Gender = application.Gender,
                    Address = application.StudentAddress,
                    Status = StudentStatus.Active,
                    AdmittedAtUtc = DateTimeOffset.UtcNow
                };
                var guardian = new Guardian
                {
                    FullName = application.GuardianFullName,
                    Relationship = application.GuardianRelationship,
                    Phone = application.GuardianPhone,
                    Email = application.GuardianEmail,
                    Address = application.GuardianAddress
                };

                db.Students.Add(student);
                db.Guardians.Add(guardian);
                db.StudentGuardians.Add(new StudentGuardian { Student = student, Guardian = guardian });
                db.Enrollments.Add(new Enrollment
                {
                    Student = student,
                    AcademicYearId = application.AcademicYearId,
                    SectionId = section.Id,
                    Status = EnrollmentStatus.Active,
                    EnrolledAtUtc = DateTimeOffset.UtcNow
                });
                db.StudentStatusHistories.Add(new StudentStatusHistory
                {
                    Student = student,
                    PreviousStatus = null,
                    NewStatus = StudentStatus.Active,
                    ChangedByUserId = reviewerId,
                    Reason = $"Admitted from application {application.ApplicationNumber}.",
                    ChangedAtUtc = DateTimeOffset.UtcNow
                });
                application.Status = AdmissionStatus.Approved;
            }
            else
            {
                application.Status = AdmissionStatus.Rejected;
            }

            db.AdmissionReviews.Add(new AdmissionReview
            {
                AdmissionApplicationId = application.Id,
                ReviewerUserId = reviewerId,
                Decision = input.Decision,
                Remarks = remarks,
                ReviewedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdmissionDecisionResult(application.ApplicationNumber, studentNumber);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PrivateDocumentDownload?> GetDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var document = await db.ApplicationDocuments.AsNoTracking()
            .Where(item => item.Id == documentId)
            .Select(item => new { item.StorageKey, item.OriginalFileName, item.ContentType })
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        var stream = await documentStore.OpenReadAsync(document.StorageKey, cancellationToken);
        return new PrivateDocumentDownload(stream, document.OriginalFileName, document.ContentType);
    }

    private static async Task<string> CreateApplicationNumberAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var number = $"APP-{DateTime.UtcNow:yyyy}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            if (!await db.AdmissionApplications.AnyAsync(application => application.ApplicationNumber == number, cancellationToken))
            {
                return number;
            }
        }

        throw new InvalidOperationException("Could not generate a unique application number. Please try again.");
    }

    private static async Task<string> CreateStudentNumberAsync(
        IApplicationDbContext db,
        int academicYear,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var number = $"STU-{academicYear}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            if (!await db.Students.AnyAsync(student => student.StudentNumber == number, cancellationToken))
            {
                return number;
            }
        }

        throw new InvalidOperationException("Could not generate a unique student number. Please try again.");
    }

    private static (int AcademicYearId, int? RequestedSectionId, string StudentFullName, DateOnly DateOfBirth,
        string Gender, string StudentAddress, string GuardianFullName, string GuardianRelationship,
        string GuardianPhone, string GuardianEmail, string GuardianAddress) ValidateSubmission(AdmissionSubmissionInput input)
    {
        if (input.AcademicYearId < 1)
        {
            throw new ArgumentException("Choose an academic year.");
        }

        var studentName = RequiredText(input.StudentFullName, "Student name", 160);
        if (input.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Date of birth must be in the past.");
        }

        var gender = OptionalText(input.Gender, "Gender", 40);
        var studentAddress = OptionalText(input.StudentAddress, "Student address", 500);
        var guardianName = RequiredText(input.GuardianFullName, "Guardian name", 160);
        var relationship = RequiredText(input.GuardianRelationship, "Relationship", 60);
        var phone = RequiredText(input.GuardianPhone, "Guardian phone", 40);
        if (!new PhoneAttribute().IsValid(phone))
        {
            throw new ArgumentException("Enter a valid guardian phone number.");
        }

        var email = OptionalText(input.GuardianEmail, "Guardian email", 254);
        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
        {
            throw new ArgumentException("Enter a valid guardian email address.");
        }

        var guardianAddress = OptionalText(input.GuardianAddress, "Guardian address", 500);
        return (input.AcademicYearId, input.RequestedSectionId, studentName, input.DateOfBirth,
            gender, studentAddress, guardianName, relationship, phone, email, guardianAddress);
    }

    private static IReadOnlyList<ValidatedDocument> ValidateDocuments(IReadOnlyCollection<AdmissionDocumentUpload> documents)
    {
        if (documents.Count > 3 || documents.Select(document => document.Type).Distinct().Count() != documents.Count)
        {
            throw new ArgumentException("Upload at most one file for each document type.");
        }

        var validated = new List<ValidatedDocument>(documents.Count);
        foreach (var document in documents)
        {
            var bytes = document.Content.Span;
            if (bytes.IsEmpty || bytes.Length > MaximumDocumentBytes)
            {
                throw new ArgumentException("Each document must be between 1 byte and 5 MB.");
            }

            var (extension, contentType, isImage) = DetectDocumentType(bytes);
            if (document.Type == AdmissionDocumentType.Photograph && !isImage)
            {
                throw new ArgumentException("A photograph must be a PNG or JPEG image.");
            }

            var fileName = SafeOriginalFileName(document.FileName, extension);
            validated.Add(new ValidatedDocument(document, fileName, extension, contentType));
        }

        return validated;
    }

    private static (string Extension, string ContentType, bool IsImage) DetectDocumentType(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 5 && Encoding.ASCII.GetString(content[..5]) == "%PDF-")
        {
            return (".pdf", "application/pdf", false);
        }

        if (content.Length >= 8 && content[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return (".png", "image/png", true);
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return (".jpg", "image/jpeg", true);
        }

        throw new ArgumentException("Documents must be PDF, PNG, or JPEG files with a valid file signature.");
    }

    private static string SafeOriginalFileName(string? fileName, string extension)
    {
        var safe = Path.GetFileName((fileName ?? "").Replace('\\', '/'));
        safe = new string(safe.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safe))
        {
            return $"document{extension}";
        }

        return safe.Length <= 180 ? safe : safe[..180];
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

    private sealed record ValidatedDocument(
        AdmissionDocumentUpload Upload,
        string FileName,
        string Extension,
        string ContentType);
}
