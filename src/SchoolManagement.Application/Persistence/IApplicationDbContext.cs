using Microsoft.EntityFrameworkCore;
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
