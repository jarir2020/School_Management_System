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
    }
}
