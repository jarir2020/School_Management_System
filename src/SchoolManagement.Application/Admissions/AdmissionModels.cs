using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Admissions;

public sealed record AdmissionSubmissionInput(
    int AcademicYearId,
    int? RequestedSectionId,
    string StudentFullName,
    DateOnly DateOfBirth,
    string Gender,
    string StudentAddress,
    string GuardianFullName,
    string GuardianRelationship,
    string GuardianPhone,
    string GuardianEmail,
    string GuardianAddress);

public sealed record AdmissionDocumentUpload(
    AdmissionDocumentType Type,
    string FileName,
    ReadOnlyMemory<byte> Content);

public sealed record AdmissionReceipt(string ApplicationNumber);

public sealed record AdmissionSearchFilter(string? Search, AdmissionStatus? Status, int Page = 1, int PageSize = 20);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record AdmissionSummary(
    int Id,
    string ApplicationNumber,
    string StudentFullName,
    string GuardianFullName,
    string GuardianPhone,
    string AcademicYearName,
    AdmissionStatus Status,
    DateTimeOffset SubmittedAtUtc,
    int DocumentCount);

public sealed record AdmissionDocumentInfo(
    int Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    AdmissionDocumentType Type,
    DateTimeOffset UploadedAtUtc);

public sealed record AdmissionReviewInfo(
    AdmissionDecision Decision,
    string ReviewerUserId,
    string Remarks,
    DateTimeOffset ReviewedAtUtc);

public sealed record AdmissionDetails(
    int Id,
    string ApplicationNumber,
    int AcademicYearId,
    string AcademicYearName,
    int? RequestedSectionId,
    string? RequestedSectionName,
    string StudentFullName,
    DateOnly DateOfBirth,
    string Gender,
    string StudentAddress,
    string GuardianFullName,
    string GuardianRelationship,
    string GuardianPhone,
    string GuardianEmail,
    string GuardianAddress,
    AdmissionStatus Status,
    DateTimeOffset SubmittedAtUtc,
    int? StudentId,
    string? StudentNumber,
    IReadOnlyList<AdmissionDocumentInfo> Documents,
    IReadOnlyList<AdmissionReviewInfo> Reviews);

public sealed record AdmissionReviewInput(AdmissionDecision Decision, int? SectionId, string? Remarks);

public sealed record AdmissionDecisionResult(string ApplicationNumber, string? StudentNumber);

public sealed record PrivateDocumentDownload(Stream Content, string FileName, string ContentType);

public sealed record StudentSearchFilter(
    string? Search,
    StudentStatus? Status,
    int? AcademicYearId,
    int? SectionId,
    int Page = 1,
    int PageSize = 20);

public sealed record StudentSummary(
    int Id,
    string StudentNumber,
    string FullName,
    DateOnly DateOfBirth,
    StudentStatus Status,
    string? SectionName,
    string? ClassName,
    string? AcademicYearName,
    string GuardianPhone);

public sealed record StudentGuardianInfo(
    int GuardianId,
    string FullName,
    string Relationship,
    string Phone,
    string Email,
    string Address);

public sealed record StudentEnrollmentInfo(
    int AcademicYearId,
    string AcademicYearName,
    int SectionId,
    string SectionName,
    string ClassName,
    EnrollmentStatus Status,
    DateTimeOffset EnrolledAtUtc);

public sealed record StudentStatusChangeInfo(
    StudentStatus? PreviousStatus,
    StudentStatus NewStatus,
    string ChangedByUserId,
    string Reason,
    DateTimeOffset ChangedAtUtc);

public sealed record StudentDetails(
    int Id,
    string StudentNumber,
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    StudentStatus Status,
    IReadOnlyList<StudentGuardianInfo> Guardians,
    IReadOnlyList<StudentEnrollmentInfo> Enrollments,
    IReadOnlyList<StudentStatusChangeInfo> StatusHistory);

public sealed record StudentUpdateInput(
    string FullName,
    DateOnly DateOfBirth,
    string Gender,
    string Address,
    string GuardianFullName,
    string GuardianRelationship,
    string GuardianPhone,
    string GuardianEmail,
    string GuardianAddress,
    StudentStatus Status,
    string StatusReason);

public sealed record StudentExportFile(byte[] Content, string FileName, string ContentType);

public sealed record AdmissionAcademicYear(int Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsActive);

public sealed record AdmissionSection(int Id, string Name, string ClassName, int Capacity, int ActiveEnrollmentCount);
