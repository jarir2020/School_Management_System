using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Files;
using SchoolManagement.Infrastructure.Services;

namespace SchoolManagement.Tests.Support;

internal sealed class SqliteSchoolDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly ServiceProvider services;
    private readonly string privateFilesPath;

    private SqliteSchoolDatabase(SqliteConnection connection, ServiceProvider services)
    {
        this.connection = connection;
        this.services = services;
        privateFilesPath = Path.Combine(Path.GetTempPath(), "school-management-tests", Guid.NewGuid().ToString("N"));
    }

    public IApplicationDbContextFactory ApplicationDbContextFactory => services.GetRequiredService<IApplicationDbContextFactory>();

    public async Task<ApplicationDbContext> CreateContextAsync(CancellationToken cancellationToken = default) =>
        await services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync(cancellationToken);

    public IServiceScope CreateScope() => services.CreateScope();

    public AdmissionService CreateAdmissionService() =>
        new(ApplicationDbContextFactory, new LocalPrivateDocumentStore(privateFilesPath));

    public async Task<ApplicationUser> CreateUserAsync(string email, params string[] roles)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in roles.Distinct(StringComparer.Ordinal))
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
                }
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, "School-Test-Password-123!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        if (roles.Length > 0)
        {
            var roleResult = await userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        return user;
    }

    public static async Task<SqliteSchoolDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContextFactory<ApplicationDbContext>(options => options
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetModelCustomizer>());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<IApplicationDbContextFactory>(provider => new TestApplicationDbContextFactory(
            provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()));

        var provider = services.BuildServiceProvider(validateScopes: true);
        var database = new SqliteSchoolDatabase(connection, provider);
        await using var db = await database.CreateContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        return database;
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        await connection.DisposeAsync();
        if (Directory.Exists(privateFilesPath)) Directory.Delete(privateFilesPath, recursive: true);
    }

    private sealed class TestApplicationDbContextFactory(IDbContextFactory<ApplicationDbContext> factory) : IApplicationDbContextFactory
    {
        public async Task<IApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            await factory.CreateDbContextAsync(cancellationToken);
    }

}

internal sealed class SqliteDateTimeOffsetModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    private static readonly ValueConverter<DateTimeOffset, long> UtcTicksConverter = new(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entity => entity.GetProperties())
                     .Where(property => property.ClrType == typeof(DateTimeOffset)))
        {
            property.SetValueConverter(UtcTicksConverter);
        }
    }
}
