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

    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
