using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using SchoolManagement.Core.Entities;

namespace SchoolManagement.Application.Persistence;

/// <summary>
/// Exposes only the sets the application layer needs, without exposing MySQL details.
/// </summary>
public interface IApplicationDbContext : IAsyncDisposable
{
    DbSet<SchoolProfile> SchoolProfiles { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<AcademicYear> AcademicYears { get; }
    DbSet<SchoolClass> Classes { get; }
    DbSet<Section> Sections { get; }
    DbSet<AdmissionApplication> AdmissionApplications { get; }
    DbSet<ApplicationDocument> ApplicationDocuments { get; }
    DbSet<AdmissionReview> AdmissionReviews { get; }
    DbSet<Student> Students { get; }
    DbSet<Guardian> Guardians { get; }
    DbSet<StudentGuardian> StudentGuardians { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<StudentStatusHistory> StudentStatusHistories { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<TeacherAssignment> TeacherAssignments { get; }
    DbSet<AttendanceSession> AttendanceSessions { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests { get; }
    DbSet<GuardianAccountLink> GuardianAccountLinks { get; }
    DbSet<Term> Terms { get; }
    DbSet<Examination> Examinations { get; }
    DbSet<ExamSubject> ExamSubjects { get; }
    DbSet<StudentMark> StudentMarks { get; }
    DbSet<ResultApproval> ResultApprovals { get; }
    DbSet<ResultRevision> ResultRevisions { get; }
    DbSet<GradeBand> GradeBands { get; }
    DbSet<FeeStructure> FeeStructures { get; }
    DbSet<StudentFeeAssignment> StudentFeeAssignments { get; }
    DbSet<FeeDiscountRequest> FeeDiscountRequests { get; }
    DbSet<FeeInvoice> FeeInvoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentAllocation> PaymentAllocations { get; }
    DbSet<PaymentReceipt> PaymentReceipts { get; }
    DbSet<RefundRequest> RefundRequests { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<SchoolMessage> Messages { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<EmailOutboxMessage> EmailOutboxMessages { get; }
    DbSet<BackgroundJobLog> BackgroundJobLogs { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
