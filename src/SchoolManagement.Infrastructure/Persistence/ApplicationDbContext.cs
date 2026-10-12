using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Data;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Entities;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<SchoolProfile> SchoolProfiles => Set<SchoolProfile>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();
    public DbSet<AdmissionReview> AdmissionReviews => Set<AdmissionReview>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<StudentStatusHistory> StudentStatusHistories => Set<StudentStatusHistory>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests => Set<AttendanceCorrectionRequest>();
    public DbSet<GuardianAccountLink> GuardianAccountLinks => Set<GuardianAccountLink>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Examination> Examinations => Set<Examination>();
    public DbSet<ExamSubject> ExamSubjects => Set<ExamSubject>();
    public DbSet<StudentMark> StudentMarks => Set<StudentMark>();
    public DbSet<ResultApproval> ResultApprovals => Set<ResultApproval>();
    public DbSet<ResultRevision> ResultRevisions => Set<ResultRevision>();
    public DbSet<GradeBand> GradeBands => Set<GradeBand>();
    public DbSet<FeeStructure> FeeStructures => Set<FeeStructure>();
    public DbSet<StudentFeeAssignment> StudentFeeAssignments => Set<StudentFeeAssignment>();
    public DbSet<FeeDiscountRequest> FeeDiscountRequests => Set<FeeDiscountRequest>();
    public DbSet<FeeInvoice> FeeInvoices => Set<FeeInvoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<SchoolMessage> Messages => Set<SchoolMessage>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<BackgroundJobLog> BackgroundJobLogs => Set<BackgroundJobLog>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasCharSet("utf8mb4");

        modelBuilder.Entity<SchoolProfile>(entity =>
        {
            entity.ToTable("SchoolProfiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Name).HasMaxLength(160).IsRequired();
            entity.Property(profile => profile.Address).HasMaxLength(500);
            entity.Property(profile => profile.Phone).HasMaxLength(40);
            entity.Property(profile => profile.Email).HasMaxLength(254);
            entity.Property(profile => profile.TimeZoneId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.UserId).HasMaxLength(450);
            entity.Property(profile => profile.FullName).HasMaxLength(160).IsRequired();
            entity.Property(profile => profile.Phone).HasMaxLength(40);
            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.ToTable("AcademicYears");
            entity.HasKey(year => year.Id);
            entity.Property(year => year.Name).HasMaxLength(32).IsRequired();
            entity.Property(year => year.StartDate).HasColumnType("date");
            entity.Property(year => year.EndDate).HasColumnType("date");
            entity.HasIndex(year => year.Name).IsUnique();
        });

        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("Classes");
            entity.HasKey(schoolClass => schoolClass.Id);
            entity.Property(schoolClass => schoolClass.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(schoolClass => schoolClass.Name).IsUnique();
            entity.HasIndex(schoolClass => schoolClass.GradeLevel).IsUnique();
            entity.HasMany(schoolClass => schoolClass.Sections)
                .WithOne(section => section.Class)
                .HasForeignKey(section => section.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Section>(entity =>
        {
            entity.ToTable("Sections");
            entity.HasKey(section => section.Id);
            entity.Property(section => section.Name).HasMaxLength(40).IsRequired();
            entity.HasIndex(section => new { section.ClassId, section.Name }).IsUnique();
        });

        modelBuilder.Entity<AdmissionApplication>(entity =>
        {
            entity.ToTable("AdmissionApplications");
            entity.HasKey(application => application.Id);
            entity.Property(application => application.ApplicationNumber).HasMaxLength(32).IsRequired();
            entity.HasIndex(application => application.ApplicationNumber).IsUnique();
            entity.Property(application => application.StudentFullName).HasMaxLength(160).IsRequired();
            entity.Property(application => application.GuardianFullName).HasMaxLength(160).IsRequired();
            entity.Property(application => application.GuardianRelationship).HasMaxLength(60).IsRequired();
            entity.Property(application => application.GuardianPhone).HasMaxLength(40).IsRequired();
            entity.Property(application => application.GuardianEmail).HasMaxLength(254);
            entity.Property(application => application.Gender).HasMaxLength(40);
            entity.Property(application => application.StudentAddress).HasMaxLength(500);
            entity.Property(application => application.GuardianAddress).HasMaxLength(500);
            entity.HasIndex(application => new { application.Status, application.SubmittedAtUtc });
            entity.HasOne(application => application.AcademicYear)
                .WithMany()
                .HasForeignKey(application => application.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(application => application.RequestedSection)
                .WithMany()
                .HasForeignKey(application => application.RequestedSectionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApplicationDocument>(entity =>
        {
            entity.ToTable("ApplicationDocuments");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.OriginalFileName).HasMaxLength(180).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(document => document.StorageKey).HasMaxLength(80).IsRequired();
            entity.HasIndex(document => document.StorageKey).IsUnique();
            entity.HasOne(document => document.AdmissionApplication)
                .WithMany(application => application.Documents)
                .HasForeignKey(document => document.AdmissionApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdmissionReview>(entity =>
        {
            entity.ToTable("AdmissionReviews");
            entity.HasKey(review => review.Id);
            entity.Property(review => review.ReviewerUserId).HasMaxLength(450).IsRequired();
            entity.Property(review => review.Remarks).HasMaxLength(1000);
            entity.HasIndex(review => new { review.AdmissionApplicationId, review.ReviewedAtUtc });
            entity.HasOne(review => review.AdmissionApplication)
                .WithMany(application => application.Reviews)
                .HasForeignKey(review => review.AdmissionApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(review => review.ReviewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(student => student.Id);
            entity.Property(student => student.StudentNumber).HasMaxLength(32).IsRequired();
            entity.Property(student => student.FullName).HasMaxLength(160).IsRequired();
            entity.Property(student => student.Gender).HasMaxLength(40);
            entity.Property(student => student.Address).HasMaxLength(500);
            entity.HasIndex(student => student.StudentNumber).IsUnique();
            entity.HasIndex(student => student.AdmissionApplicationId).IsUnique();
            entity.HasIndex(student => new { student.Status, student.FullName });
            entity.HasOne(student => student.AdmissionApplication)
                .WithOne(application => application.Student)
                .HasForeignKey<Student>(student => student.AdmissionApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Guardian>(entity =>
        {
            entity.ToTable("Guardians");
            entity.HasKey(guardian => guardian.Id);
            entity.Property(guardian => guardian.FullName).HasMaxLength(160).IsRequired();
            entity.Property(guardian => guardian.Relationship).HasMaxLength(60).IsRequired();
            entity.Property(guardian => guardian.Phone).HasMaxLength(40).IsRequired();
            entity.Property(guardian => guardian.Email).HasMaxLength(254);
            entity.Property(guardian => guardian.Address).HasMaxLength(500);
            entity.HasIndex(guardian => guardian.Phone);
        });

        modelBuilder.Entity<StudentGuardian>(entity =>
        {
            entity.ToTable("StudentGuardians");
            entity.HasKey(link => new { link.StudentId, link.GuardianId });
            entity.HasOne(link => link.Student)
                .WithMany(student => student.Guardians)
                .HasForeignKey(link => link.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(link => link.Guardian)
                .WithMany(guardian => guardian.Students)
                .HasForeignKey(link => link.GuardianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("Enrollments");
            entity.HasKey(enrollment => enrollment.Id);
            entity.HasIndex(enrollment => new { enrollment.StudentId, enrollment.AcademicYearId }).IsUnique();
            entity.HasIndex(enrollment => new { enrollment.SectionId, enrollment.AcademicYearId, enrollment.Status });
            entity.HasOne(enrollment => enrollment.Student)
                .WithMany(student => student.Enrollments)
                .HasForeignKey(enrollment => enrollment.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(enrollment => enrollment.AcademicYear)
                .WithMany()
                .HasForeignKey(enrollment => enrollment.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(enrollment => enrollment.Section)
                .WithMany()
                .HasForeignKey(enrollment => enrollment.SectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentStatusHistory>(entity =>
        {
            entity.ToTable("StudentStatusHistory");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.ChangedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(history => history.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(history => new { history.StudentId, history.ChangedAtUtc });
            entity.HasOne(history => history.Student)
                .WithMany(student => student.StatusHistory)
                .HasForeignKey(history => history.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(history => history.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");
            entity.HasKey(subject => subject.Id);
            entity.Property(subject => subject.Name).HasMaxLength(100).IsRequired();
            entity.Property(subject => subject.Code).HasMaxLength(24).IsRequired();
            entity.HasIndex(subject => subject.Code).IsUnique();
        });

        modelBuilder.Entity<TeacherAssignment>(entity =>
        {
            entity.ToTable("TeacherAssignments");
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.TeacherUserId).HasMaxLength(450).IsRequired();
            entity.Property(assignment => assignment.AssignedByUserId).HasMaxLength(450);
            entity.HasIndex(assignment => new
            {
                assignment.TeacherUserId,
                assignment.SectionId,
                assignment.SubjectId,
                assignment.AcademicYearId
            }).IsUnique();
            entity.HasIndex(assignment => new { assignment.SectionId, assignment.AcademicYearId });
            entity.HasOne(assignment => assignment.Section)
                .WithMany()
                .HasForeignKey(assignment => assignment.SectionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(assignment => assignment.Subject)
                .WithMany()
                .HasForeignKey(assignment => assignment.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(assignment => assignment.AcademicYear)
                .WithMany()
                .HasForeignKey(assignment => assignment.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(assignment => assignment.TeacherUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(assignment => assignment.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceSession>(entity =>
        {
            entity.ToTable("AttendanceSessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.TeacherUserId).HasMaxLength(450).IsRequired();
            entity.Property(session => session.AttendanceDate).HasColumnType("date");
            entity.HasIndex(session => new
            {
                session.SectionId,
                session.SubjectId,
                session.AcademicYearId,
                session.AttendanceDate
            }).IsUnique();
            entity.HasIndex(session => new { session.TeacherUserId, session.AttendanceDate });
            entity.HasOne(session => session.Section)
                .WithMany()
                .HasForeignKey(session => session.SectionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.Subject)
                .WithMany()
                .HasForeignKey(session => session.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.AcademicYear)
                .WithMany()
                .HasForeignKey(session => session.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(session => session.TeacherUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(record => record.Note).HasMaxLength(300).IsRequired();
            entity.HasIndex(record => new { record.AttendanceSessionId, record.StudentId }).IsUnique();
            entity.HasIndex(record => record.StudentId);
            entity.HasOne(record => record.AttendanceSession)
                .WithMany(session => session.Records)
                .HasForeignKey(record => record.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(record => record.Student)
                .WithMany()
                .HasForeignKey(record => record.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceCorrectionRequest>(entity =>
        {
            entity.ToTable("AttendanceCorrectionRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.PreviousStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(request => request.RequestedStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(request => request.Reason).HasMaxLength(500).IsRequired();
            entity.Property(request => request.RequestedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(request => request.ReviewedByUserId).HasMaxLength(450);
            entity.Property(request => request.ReviewerRemarks).HasMaxLength(500);
            entity.HasIndex(request => new { request.AttendanceRecordId, request.Status });
            entity.HasOne(request => request.AttendanceRecord)
                .WithMany()
                .HasForeignKey(request => request.AttendanceRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(request => request.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(request => request.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GuardianAccountLink>(entity =>
        {
            entity.ToTable("GuardianAccountLinks");
            entity.HasKey(link => new { link.GuardianId, link.UserId });
            entity.Property(link => link.UserId).HasMaxLength(450).IsRequired();
            entity.Property(link => link.LinkedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(link => link.UserId);
            entity.HasOne(link => link.Guardian)
                .WithMany()
                .HasForeignKey(link => link.GuardianId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(link => link.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(link => link.LinkedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Term>(entity =>
        {
            entity.ToTable("Terms");
            entity.HasKey(term => term.Id);
            entity.Property(term => term.Name).HasMaxLength(60).IsRequired();
            entity.Property(term => term.StartDate).HasColumnType("date");
            entity.Property(term => term.EndDate).HasColumnType("date");
            entity.HasIndex(term => new { term.AcademicYearId, term.Name }).IsUnique();
            entity.HasOne(term => term.AcademicYear)
                .WithMany()
                .HasForeignKey(term => term.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Examination>(entity =>
        {
            entity.ToTable("Examinations");
            entity.HasKey(exam => exam.Id);
            entity.Property(exam => exam.Name).HasMaxLength(120).IsRequired();
            entity.Property(exam => exam.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(exam => exam.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(exam => new { exam.AcademicYearId, exam.TermId, exam.Name }).IsUnique();
            entity.HasOne(exam => exam.AcademicYear)
                .WithMany()
                .HasForeignKey(exam => exam.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(exam => exam.Term)
                .WithMany()
                .HasForeignKey(exam => exam.TermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(exam => exam.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamSubject>(entity =>
        {
            entity.ToTable("ExamSubjects");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FullMarks).HasPrecision(7, 2);
            entity.Property(item => item.PassMarks).HasPrecision(7, 2);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(item => new { item.ExaminationId, item.Status });
            entity.HasIndex(item => new { item.ExaminationId, item.SectionId, item.SubjectId }).IsUnique();
            entity.HasOne(item => item.Examination)
                .WithMany(exam => exam.Subjects)
                .HasForeignKey(item => item.ExaminationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Section)
                .WithMany()
                .HasForeignKey(item => item.SectionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Subject)
                .WithMany()
                .HasForeignKey(item => item.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentMark>(entity =>
        {
            entity.ToTable("StudentMarks");
            entity.HasKey(mark => mark.Id);
            entity.Property(mark => mark.MarksObtained).HasPrecision(7, 2);
            entity.Property(mark => mark.EnteredByUserId).HasMaxLength(450).IsRequired();
            entity.Property(mark => mark.UpdatedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(mark => new { mark.ExamSubjectId, mark.StudentId }).IsUnique();
            entity.HasIndex(mark => mark.StudentId);
            entity.HasOne(mark => mark.ExamSubject)
                .WithMany(item => item.Marks)
                .HasForeignKey(mark => mark.ExamSubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(mark => mark.Student)
                .WithMany()
                .HasForeignKey(mark => mark.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(mark => mark.EnteredByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(mark => mark.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ResultApproval>(entity =>
        {
            entity.ToTable("ResultApprovals");
            entity.HasKey(approval => approval.Id);
            entity.Property(approval => approval.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(approval => approval.ReviewerUserId).HasMaxLength(450).IsRequired();
            entity.Property(approval => approval.Remarks).HasMaxLength(1000).IsRequired();
            entity.HasIndex(approval => new { approval.ExaminationId, approval.ReviewedAtUtc });
            entity.HasOne(approval => approval.Examination)
                .WithMany(exam => exam.Approvals)
                .HasForeignKey(approval => approval.ExaminationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(approval => approval.ReviewerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ResultRevision>(entity =>
        {
            entity.ToTable("ResultRevisions");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.PreviousMarks).HasPrecision(7, 2);
            entity.Property(revision => revision.NewMarks).HasPrecision(7, 2);
            entity.Property(revision => revision.Reason).HasMaxLength(500).IsRequired();
            entity.Property(revision => revision.RevisedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(revision => new { revision.StudentMarkId, revision.RevisedAtUtc });
            entity.HasOne(revision => revision.StudentMark)
                .WithMany(mark => mark.Revisions)
                .HasForeignKey(revision => revision.StudentMarkId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(revision => revision.RevisedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GradeBand>(entity =>
        {
            entity.ToTable("GradeBands");
            entity.HasKey(band => band.Id);
            entity.Property(band => band.MinimumPercentage).HasPrecision(5, 2);
            entity.Property(band => band.LetterGrade).HasMaxLength(8).IsRequired();
            entity.Property(band => band.GradePoint).HasPrecision(4, 2);
            entity.Property(band => band.Remark).HasMaxLength(120).IsRequired();
            entity.HasIndex(band => new { band.AcademicYearId, band.MinimumPercentage }).IsUnique();
            entity.HasOne(band => band.AcademicYear)
                .WithMany()
                .HasForeignKey(band => band.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeeStructure>(entity =>
        {
            entity.ToTable("FeeStructures");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.DueDate).HasColumnType("date");
            entity.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(item => new { item.AcademicYearId, item.SchoolClassId, item.Name }).IsUnique();
            entity.HasOne(item => item.AcademicYear)
                .WithMany()
                .HasForeignKey(item => item.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SchoolClass)
                .WithMany()
                .HasForeignKey(item => item.SchoolClassId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentFeeAssignment>(entity =>
        {
            entity.ToTable("StudentFeeAssignments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.DueDate).HasColumnType("date");
            entity.Property(item => item.AssignedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(item => new { item.FeeStructureId, item.EnrollmentId }).IsUnique();
            entity.HasOne(item => item.FeeStructure)
                .WithMany(structure => structure.Assignments)
                .HasForeignKey(item => item.FeeStructureId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Enrollment)
                .WithMany()
                .HasForeignKey(item => item.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeeDiscountRequest>(entity =>
        {
            entity.ToTable("FeeDiscountRequests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(item => item.RequestedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.ReviewedByUserId).HasMaxLength(450);
            entity.Property(item => item.ReviewRemarks).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => new { item.StudentFeeAssignmentId, item.Status });
            entity.HasOne(item => item.StudentFeeAssignment)
                .WithMany(assignment => assignment.DiscountRequests)
                .HasForeignKey(item => item.StudentFeeAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeeInvoice>(entity =>
        {
            entity.ToTable("FeeInvoices");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.InvoiceNumber).HasMaxLength(40).IsRequired();
            entity.Property(item => item.FeeName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(item => item.GrossAmount).HasPrecision(12, 2);
            entity.Property(item => item.DiscountAmount).HasPrecision(12, 2);
            entity.Property(item => item.TotalAmount).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.DueDate).HasColumnType("date");
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(item => item.InvoiceNumber).IsUnique();
            entity.HasIndex(item => new { item.StudentId, item.DueDate, item.Status });
            entity.HasIndex(item => item.StudentFeeAssignmentId).IsUnique();
            entity.HasOne(item => item.StudentFeeAssignment)
                .WithOne(assignment => assignment.Invoice)
                .HasForeignKey<FeeInvoice>(item => item.StudentFeeAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Student)
                .WithMany()
                .HasForeignKey(item => item.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.GatewayReceivedAmount).HasPrecision(12, 2);
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(item => item.Reference).HasMaxLength(100);
            entity.Property(item => item.GatewayName).HasMaxLength(30).IsRequired();
            entity.Property(item => item.GatewayTransactionId).HasMaxLength(30);
            entity.Property(item => item.GatewaySessionKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.GatewayValidationId).HasMaxLength(50);
            entity.Property(item => item.GatewayBankTransactionId).HasMaxLength(80).IsRequired();
            entity.Property(item => item.GatewayRiskLevel).HasMaxLength(4).IsRequired();
            entity.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(item => item.GatewayTransactionId).IsUnique();
            entity.HasIndex(item => item.GatewayValidationId).IsUnique();
            entity.HasIndex(item => new { item.Method, item.Reference }).IsUnique();
            entity.HasIndex(item => new { item.FeeInvoiceId, item.Status });
            entity.HasOne(item => item.FeeInvoice)
                .WithMany(invoice => invoice.Payments)
                .HasForeignKey(item => item.FeeInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("PaymentAllocations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.HasIndex(item => item.PaymentId).IsUnique();
            entity.HasIndex(item => item.FeeInvoiceId);
            entity.HasOne(item => item.Payment)
                .WithMany(payment => payment.Allocations)
                .HasForeignKey(item => item.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FeeInvoice)
                .WithMany(invoice => invoice.Allocations)
                .HasForeignKey(item => item.FeeInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentReceipt>(entity =>
        {
            entity.ToTable("PaymentReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ReceiptNumber).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => item.ReceiptNumber).IsUnique();
            entity.HasIndex(item => item.PaymentId).IsUnique();
            entity.HasOne(item => item.Payment)
                .WithOne(payment => payment.Receipt)
                .HasForeignKey<PaymentReceipt>(item => item.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.ToTable("RefundRequests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Amount).HasPrecision(12, 2);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(item => item.RequestedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.ReviewedByUserId).HasMaxLength(450);
            entity.Property(item => item.ReviewRemarks).HasMaxLength(500).IsRequired();
            entity.Property(item => item.GatewayRefundTransactionId).HasMaxLength(30);
            entity.Property(item => item.GatewayRefundReference).HasMaxLength(50).IsRequired();
            entity.Property(item => item.GatewayRefundStatus).HasMaxLength(30).IsRequired();
            entity.Property(item => item.ManualRefundReference).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.PaymentId, item.Status });
            entity.HasIndex(item => item.GatewayRefundTransactionId).IsUnique();
            entity.HasOne(item => item.Payment)
                .WithMany(payment => payment.RefundRequests)
                .HasForeignKey(item => item.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Content).HasMaxLength(8000).IsRequired();
            entity.Property(item => item.Audience).HasMaxLength(20).IsRequired();
            entity.Property(item => item.PublishedByUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(item => new { item.Audience, item.PublishedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ParentUserId).HasMaxLength(255).IsRequired();
            entity.Property(item => item.TeacherUserId).HasMaxLength(255).IsRequired();
            entity.HasIndex(item => new { item.StudentId, item.ParentUserId, item.TeacherUserId }).IsUnique();
            entity.HasIndex(item => new { item.ParentUserId, item.LastMessageAtUtc });
            entity.HasIndex(item => new { item.TeacherUserId, item.LastMessageAtUtc });
            entity.HasOne(item => item.Student).WithMany().HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ParentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.TeacherUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SchoolMessage>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SenderUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.Content).HasMaxLength(4000).IsRequired();
            entity.HasIndex(item => new { item.ConversationId, item.SentAtUtc });
            entity.HasOne(item => item.Conversation).WithMany(conversation => conversation.Messages).HasForeignKey(item => item.ConversationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.ToTable("MessageAttachments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OriginalFileName).HasMaxLength(180).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.StorageKey).IsUnique();
            entity.HasOne(item => item.Message).WithMany(message => message.Attachments).HasForeignKey(item => item.MessageId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActorUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Details).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.OccurredAtUtc);
            entity.HasIndex(item => new { item.EntityType, item.EntityId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.ToTable("EmailOutboxMessages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Recipient).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(180).IsRequired();
            entity.Property(item => item.HtmlBody).HasMaxLength(12000).IsRequired();
            entity.Property(item => item.DeduplicationKey).HasMaxLength(220).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.DeduplicationKey).IsUnique();
            entity.HasIndex(item => new { item.Status, item.NextAttemptAtUtc });
        });

        modelBuilder.Entity<BackgroundJobLog>(entity =>
        {
            entity.ToTable("BackgroundJobLogs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.JobType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Details).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.RecordedAtUtc);
        });
    }
}
