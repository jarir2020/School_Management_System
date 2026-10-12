using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Persistence;

namespace SchoolManagement.Infrastructure.Persistence;

internal sealed class ApplicationDbContextFactoryAdapter(
    IDbContextFactory<ApplicationDbContext> contextFactory) : IApplicationDbContextFactory
{
    public async Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await contextFactory.CreateDbContextAsync(cancellationToken);
    }
}
