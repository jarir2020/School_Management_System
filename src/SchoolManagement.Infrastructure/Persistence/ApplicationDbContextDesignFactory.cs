using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Lets EF Core create migrations without opening a live database connection.
/// </summary>
public sealed class ApplicationDbContextDesignFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // EF migrations need provider metadata at design time; database update scripts supply the real connection string explicitly.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SchoolManagement")
            ?? "Server=localhost;Port=3306;Database=school_management;";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)),
                mysql => mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
