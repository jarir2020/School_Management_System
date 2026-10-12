using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Administration;
using SchoolManagement.Core.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services;

public sealed partial class PhaseSixService : IPhaseSixService
{
    private readonly IDbContextFactory<ApplicationDbContext> contextFactory;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly SchoolManagement.Application.Admissions.IPrivateDocumentStore documentStore;
    private readonly SchoolManagement.Application.Finance.IFinanceService financeService;
    private readonly SchoolManagement.Application.Examinations.IExaminationService examinationService;
    private readonly SmtpEmailOptions emailOptions;
    private readonly ILogger<PhaseSixService> logger;
    private const int MaximumUploadBytes = 5 * 1024 * 1024;

    public PhaseSixService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        UserManager<ApplicationUser> userManager,
        SchoolManagement.Application.Admissions.IPrivateDocumentStore documentStore,
        SchoolManagement.Application.Finance.IFinanceService financeService,
        SchoolManagement.Application.Examinations.IExaminationService examinationService,
        IOptions<SmtpEmailOptions> smtpOptions,
        ILogger<PhaseSixService> logger)
    {
        this.contextFactory = contextFactory;
        this.userManager = userManager;
        this.documentStore = documentStore;
        this.financeService = financeService;
        this.examinationService = examinationService;
        emailOptions = smtpOptions.Value;
        this.logger = logger;
    }

    private async Task EnsureRoleAsync(string actorUserId, CancellationToken cancellationToken, params string[] roles)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(actorUserId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        foreach (var role in roles)
        {
            if (await userManager.IsInRoleAsync(user, role)) return;
        }

        throw new UnauthorizedAccessException("Your account does not have permission to perform this action.");
    }

    private static AuditLog AddAudit(
        ApplicationDbContext db,
        string actorUserId,
        string action,
        string entityType,
        string entityId,
        string details = "")
    {
        var entry = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
        db.AuditLogs.Add(entry);
        return entry;
    }

    private Task<AuditLog> AddAuditAsync(
        ApplicationDbContext db,
        string actorUserId,
        string action,
        string entityType,
        string entityId,
        string details = "") => Task.FromResult(AddAudit(db, actorUserId, action, entityType, entityId, details));

    private async Task RecordBackgroundJobAsync(
        ApplicationDbContext db,
        string type,
        string status,
        string details,
        int attemptCount = 0,
        CancellationToken cancellationToken = default)
    {
        db.BackgroundJobLogs.Add(new BackgroundJobLog
        {
            JobType = type,
            Status = status,
            Details = details,
            AttemptCount = attemptCount,
            RecordedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string SafeFileName(string fileName)
    {
        var leaf = Path.GetFileName(fileName.Trim());
        var safe = new string(leaf.Where(character => !char.IsControl(character) && character is not '/' and not '\\').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "download" : safe[..Math.Min(safe.Length, 160)];
    }
}
