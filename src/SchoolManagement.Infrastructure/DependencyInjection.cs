using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.SchoolSetup;
using SchoolManagement.Application.UserManagement;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Files;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Seeding;
using SchoolManagement.Infrastructure.Services;

namespace SchoolManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchoolManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("SchoolManagement");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:SchoolManagement in local user secrets or the environment before starting the application.");
        }

        // MySQL 8.4 is the project's selected server version; AutoDetect would connect during startup.
        var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseMySql(
                connectionString,
                serverVersion,
                mysql => mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactoryAdapter>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<ISchoolSetupService, SchoolSetupService>();
        services.AddScoped<IAdmissionService, AdmissionService>();
        services.AddScoped<IStudentDirectoryService, StudentDirectoryService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<InitialDataSeeder>();

        var privateDocumentRoot = configuration["Storage:PrivateDocumentRoot"]
            ?? Path.Combine(contentRootPath, "..", "..", ".local", "private-documents");
        services.AddSingleton<IPrivateDocumentStore>(
            new LocalPrivateDocumentStore(Path.GetFullPath(privateDocumentRoot, contentRootPath)));

        return services;
    }
}
