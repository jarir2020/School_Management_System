namespace SchoolManagement.Application.Persistence;

public interface IApplicationDbContextFactory
{
    Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
