using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Administration;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services;

public sealed partial class PhaseSixService
{
    public async Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolManagement.Core.Authorization.SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);
        var actorIds = items.Select(item => item.ActorUserId).Distinct().ToArray();
        var profiles = await db.UserProfiles.AsNoTracking().Where(item => actorIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.FullName, cancellationToken);
        var users = await db.Users.AsNoTracking().Where(item => actorIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.UserName ?? item.Email ?? "School account", cancellationToken);
        return items.Select(item => new AuditLogSummary(
            item.Id,
            profiles.GetValueOrDefault(item.ActorUserId, users.GetValueOrDefault(item.ActorUserId, "School account")),
            item.Action,
            item.EntityType,
            item.EntityId,
            item.Details,
            item.OccurredAtUtc)).ToArray();
    }

    public async Task<IReadOnlyList<BackgroundJobSummary>> GetBackgroundJobLogsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolManagement.Core.Authorization.SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BackgroundJobLogs.AsNoTracking()
            .OrderByDescending(item => item.RecordedAtUtc)
            .Take(300)
            .Select(item => new BackgroundJobSummary(item.Id, item.JobType, item.Status, item.Details, item.AttemptCount, item.RecordedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task QueueEmailAsync(
        string recipient,
        string subject,
        string htmlBody,
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedRecipient = recipient.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(normalizedRecipient, out _))
            throw new ArgumentException("Enter a valid email recipient.", nameof(recipient));
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 180)
            throw new ArgumentException("Email subject is required and must be at most 180 characters.", nameof(subject));
        if (string.IsNullOrWhiteSpace(htmlBody) || htmlBody.Length > 12000)
            throw new ArgumentException("Email content is required and must be at most 12,000 characters.", nameof(htmlBody));
        if (string.IsNullOrWhiteSpace(deduplicationKey) || deduplicationKey.Length > 220)
            throw new ArgumentException("Email queue key is invalid.", nameof(deduplicationKey));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.EmailOutboxMessages.AnyAsync(item => item.DeduplicationKey == deduplicationKey, cancellationToken)) return;
        db.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            Recipient = normalizedRecipient,
            Subject = subject.Trim(),
            HtmlBody = htmlBody,
            DeduplicationKey = deduplicationKey,
            Status = "Pending",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task QueueOverdueReminderEmailsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var timeZoneId = await db.SchoolProfiles.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => item.TimeZoneId).FirstOrDefaultAsync(cancellationToken);
        var timeZone = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        var invoices = await db.FeeInvoices.AsNoTracking()
            .Where(invoice => invoice.Status == FeeInvoiceStatus.Issued && invoice.DueDate < today)
            .Include(invoice => invoice.Student).ThenInclude(student => student!.Guardians).ThenInclude(link => link.Guardian)
            .Include(invoice => invoice.Payments).ThenInclude(payment => payment.Allocations)
            .Include(invoice => invoice.Payments).ThenInclude(payment => payment.RefundRequests)
            .ToListAsync(cancellationToken);
        var queued = 0;
        foreach (var invoice in invoices)
        {
            var succeeded = invoice.Payments.Where(payment => payment.Status == PaymentStatus.Succeeded).ToArray();
            var received = succeeded.SelectMany(payment => payment.Allocations).Sum(allocation => allocation.Amount);
            var refunded = succeeded.SelectMany(payment => payment.RefundRequests)
                .Where(request => request.Status == RefundStatus.Completed).Sum(request => request.Amount);
            var balance = Math.Max(invoice.TotalAmount - received + refunded, 0m);
            if (balance <= 0) continue;

            var contacts = invoice.Student?.Guardians.Select(link => link.Guardian)
                .Where(guardian => guardian is not null && System.Net.Mail.MailAddress.TryCreate(guardian.Email, out _))
                .Select(guardian => guardian!)
                .DistinctBy(guardian => guardian.Email.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
            foreach (var guardian in contacts)
            {
                var email = guardian.Email.Trim();
                var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant())))[..16];
                var key = $"overdue-reminder:{invoice.Id}:{keyHash}";
                if (await db.EmailOutboxMessages.AnyAsync(item => item.DeduplicationKey == key, cancellationToken)) continue;
                var studentName = WebUtility.HtmlEncode(invoice.Student?.FullName ?? "Student");
                var invoiceNumber = WebUtility.HtmlEncode(invoice.InvoiceNumber);
                var feeName = WebUtility.HtmlEncode(invoice.FeeName);
                var body = $"<p>Dear {WebUtility.HtmlEncode(guardian.FullName)},</p><p>The balance for <strong>{feeName}</strong> (invoice {invoiceNumber}) is overdue.</p><p>Student: {studentName}<br>Due date: {invoice.DueDate:yyyy-MM-dd}<br>Balance: {balance:N2} {WebUtility.HtmlEncode(invoice.Currency)}</p><p>Please contact the school office if you have already paid or need help.</p>";
                db.EmailOutboxMessages.Add(new EmailOutboxMessage
                {
                    Recipient = email,
                    Subject = $"Overdue school fee reminder — {invoiceNumber}",
                    HtmlBody = body,
                    DeduplicationKey = key,
                    Status = "Pending",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    NextAttemptAtUtc = DateTimeOffset.UtcNow
                });
                queued++;
            }
        }

        if (queued > 0) await db.SaveChangesAsync(cancellationToken);
        await RecordBackgroundJobAsync(db, "OverdueReminderSweep", "Succeeded", $"Queued {queued} overdue reminder email(s).", cancellationToken: cancellationToken);
    }

    public async Task<int> DispatchPendingEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (!emailOptions.IsConfigured) return 0;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var batch = await db.EmailOutboxMessages
            .Where(item => item.Status == "Pending" && item.NextAttemptAtUtc <= now && item.AttemptCount < 5)
            .OrderBy(item => item.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var item in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            item.AttemptCount++;
            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(emailOptions.FromAddress, emailOptions.FromName),
                    Subject = item.Subject,
                    Body = item.HtmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(item.Recipient);
                using var client = new SmtpClient(emailOptions.Host, emailOptions.Port)
                {
                    EnableSsl = emailOptions.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };
                if (!string.IsNullOrWhiteSpace(emailOptions.UserName))
                    client.Credentials = new NetworkCredential(emailOptions.UserName, emailOptions.Password);
                await client.SendMailAsync(message, cancellationToken);
                item.Status = "Sent";
                item.SentAtUtc = DateTimeOffset.UtcNow;
                item.LastError = "";
                sent++;
                await RecordBackgroundJobAsync(db, "EmailDelivery", "Succeeded", "Delivered one queued email.", item.AttemptCount, cancellationToken);
            }
            catch (Exception exception) when (exception is SmtpException or InvalidOperationException or OperationCanceledException)
            {
                item.Status = item.AttemptCount >= 5 ? "Failed" : "Pending";
                item.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(Math.Min(5 * item.AttemptCount, 240));
                item.LastError = exception.GetType().Name;
                await RecordBackgroundJobAsync(db, "EmailDelivery", item.Status, $"SMTP delivery failed ({exception.GetType().Name}).", item.AttemptCount, cancellationToken);
                logger.LogWarning("Queued email delivery attempt failed with {ExceptionType}; attempt {Attempt}.", exception.GetType().Name, item.AttemptCount);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        return sent;
    }
}

public sealed class PhaseSixBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PhaseSixBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextReminderSweep = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var phaseSix = scope.ServiceProvider.GetRequiredService<IPhaseSixService>();
                if (DateTimeOffset.UtcNow >= nextReminderSweep)
                {
                    await phaseSix.QueueOverdueReminderEmailsAsync(stoppingToken);
                    nextReminderSweep = DateTimeOffset.UtcNow.AddHours(24);
                }
                await phaseSix.DispatchPendingEmailsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The Phase 6 background worker failed. It will retry on the next interval.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
