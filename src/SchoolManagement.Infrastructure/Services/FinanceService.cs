using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SchoolManagement.Application.Finance;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Services;

/// <summary>Applies the school's fee, collection, discount, refund, and parent-access rules.</summary>
public sealed class FinanceService(
    IApplicationDbContextFactory contextFactory,
    UserManager<ApplicationUser> userManager,
    ISslCommerzGateway gateway,
    IConfiguration configuration) : IFinanceService
{
    private const string CurrencyCode = "BDT";
    private static readonly string[] FinanceStaff = [SchoolRoles.Accountant, SchoolRoles.SuperAdmin];
    private static readonly string[] FinanceReviewers = [SchoolRoles.Principal, SchoolRoles.SuperAdmin];
    private static readonly string[] FinanceReaders = [SchoolRoles.Accountant, SchoolRoles.Principal, SchoolRoles.SuperAdmin];

    public async Task<FinanceSetupOptions> GetSetupOptionsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var years = await db.AcademicYears.AsNoTracking()
            .OrderByDescending(year => year.StartDate)
            .Select(year => new FinanceYearOption(year.Id, year.Name))
            .ToListAsync(cancellationToken);
        var classes = await db.Classes.AsNoTracking()
            .OrderBy(item => item.GradeLevel)
            .Select(item => new FinanceClassOption(item.Id, item.Name, item.GradeLevel))
            .ToListAsync(cancellationToken);
        var structures = await db.FeeStructures.AsNoTracking()
            .Include(item => item.AcademicYear)
            .Include(item => item.SchoolClass)
            .Include(item => item.Assignments)
            .OrderByDescending(item => item.AcademicYear!.StartDate)
            .ThenBy(item => item.SchoolClass!.GradeLevel)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var structureIds = structures.Select(item => item.Id).ToArray();
        var billedAssignmentIds = await db.FeeInvoices.AsNoTracking()
            .Where(invoice => db.StudentFeeAssignments.Any(assignment =>
                assignment.Id == invoice.StudentFeeAssignmentId && structureIds.Contains(assignment.FeeStructureId)))
            .Select(invoice => invoice.StudentFeeAssignmentId)
            .ToListAsync(cancellationToken);
        var billed = billedAssignmentIds.ToHashSet();
        return new FinanceSetupOptions(
            years,
            classes,
            structures.Select(item => new FeeStructureSummary(
                item.Id,
                item.AcademicYearId,
                item.AcademicYear?.Name ?? "",
                item.SchoolClassId,
                item.SchoolClass?.Name ?? "",
                item.Category,
                item.Name,
                item.Amount,
                item.DueDate,
                item.IsActive,
                item.Assignments.Count,
                item.Assignments.Count(assignment => !billed.Contains(assignment.Id))))
                .ToArray());
    }

    public async Task CreateFeeStructureAsync(
        string actorUserId,
        FeeStructureInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        var name = RequiredText(input.Name, "Fee name", 100);
        ValidateMoney(input.Amount, "Fee amount");
        if (!Enum.IsDefined(input.Category)) throw new ArgumentOutOfRangeException(nameof(input.Category));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var year = await db.AcademicYears.SingleOrDefaultAsync(item => item.Id == input.AcademicYearId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected academic year does not exist.");
        if (!await db.Classes.AnyAsync(item => item.Id == input.SchoolClassId, cancellationToken))
        {
            throw new KeyNotFoundException("The selected class does not exist.");
        }

        if (input.DueDate < year.StartDate || input.DueDate > year.EndDate)
        {
            throw new ArgumentException("The fee due date must fall inside the selected academic year.");
        }

        if (await db.FeeStructures.AnyAsync(item => item.AcademicYearId == input.AcademicYearId
                && item.SchoolClassId == input.SchoolClassId && item.Name == name,
                cancellationToken))
        {
            throw new InvalidOperationException("A fee with that name already exists for this class and academic year.");
        }

        db.FeeStructures.Add(new FeeStructure
        {
            AcademicYearId = year.Id,
            SchoolClassId = input.SchoolClassId,
            Category = input.Category,
            Name = name,
            Amount = input.Amount,
            DueDate = input.DueDate,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AssignFeeStructureAsync(
        string actorUserId,
        int feeStructureId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var structure = await db.FeeStructures.SingleOrDefaultAsync(item => item.Id == feeStructureId, cancellationToken)
            ?? throw new KeyNotFoundException("The fee structure does not exist.");
        if (!structure.IsActive) throw new InvalidOperationException("This fee structure is inactive.");

        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.AcademicYearId == structure.AcademicYearId
                && enrollment.Status == EnrollmentStatus.Active
                && enrollment.Student!.Status == StudentStatus.Active
                && enrollment.Section!.ClassId == structure.SchoolClassId)
            .Select(enrollment => enrollment.Id)
            .ToListAsync(cancellationToken);
        var existing = await db.StudentFeeAssignments
            .Where(assignment => assignment.FeeStructureId == feeStructureId)
            .Select(assignment => assignment.EnrollmentId)
            .ToListAsync(cancellationToken);
        var assigned = existing.ToHashSet();
        var created = 0;
        foreach (var enrollmentId in enrollments)
        {
            if (!assigned.Add(enrollmentId)) continue;
            db.StudentFeeAssignments.Add(new StudentFeeAssignment
            {
                FeeStructureId = structure.Id,
                EnrollmentId = enrollmentId,
                Amount = structure.Amount,
                DueDate = structure.DueDate,
                AssignedByUserId = actorUserId,
                AssignedAtUtc = DateTimeOffset.UtcNow
            });
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<IReadOnlyList<FeeAssignmentSummary>> GetAssignmentsAsync(
        string actorUserId,
        int feeStructureId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assignments = await db.StudentFeeAssignments.AsNoTracking()
            .Where(item => item.FeeStructureId == feeStructureId)
            .Include(item => item.FeeStructure)
            .Include(item => item.Enrollment)!.ThenInclude(enrollment => enrollment!.Student)
            .Include(item => item.Enrollment)!.ThenInclude(enrollment => enrollment!.Section)!.ThenInclude(section => section!.Class)
            .Include(item => item.DiscountRequests)
            .Include(item => item.Invoice)
            .OrderBy(item => item.Enrollment!.Student!.FullName)
            .ToListAsync(cancellationToken);
        return assignments.Select(item => new FeeAssignmentSummary(
                item.Id,
                item.Enrollment?.StudentId ?? 0,
                item.Enrollment?.Student?.StudentNumber ?? "",
                item.Enrollment?.Student?.FullName ?? "Student",
                item.Enrollment?.Section?.Class?.Name ?? "",
                item.Enrollment?.Section?.Name ?? "",
                item.FeeStructure?.Name ?? "",
                item.Amount,
                item.DueDate,
                item.DiscountRequests.Where(request => request.Status == FeeDiscountStatus.Approved).Sum(request => request.Amount),
                item.DiscountRequests.Count(request => request.Status == FeeDiscountStatus.PendingReview),
                item.Invoice?.Id,
                item.Invoice?.InvoiceNumber ?? ""))
            .ToArray();
    }

    public async Task<int> IssueInvoicesAsync(
        string actorUserId,
        int feeStructureId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var structure = await db.FeeStructures.SingleOrDefaultAsync(item => item.Id == feeStructureId, cancellationToken)
            ?? throw new KeyNotFoundException("The fee structure does not exist.");
        if (!structure.IsActive) throw new InvalidOperationException("This fee structure is inactive and cannot issue invoices.");
        var assignments = await db.StudentFeeAssignments
            .Where(assignment => assignment.FeeStructureId == feeStructureId
                && !db.FeeInvoices.Any(invoice => invoice.StudentFeeAssignmentId == assignment.Id))
            .Include(assignment => assignment.DiscountRequests)
            .Include(assignment => assignment.Enrollment)
            .ToListAsync(cancellationToken);
        var issued = 0;
        foreach (var assignment in assignments)
        {
            if (assignment.DiscountRequests.Any(request => request.Status == FeeDiscountStatus.PendingReview)) continue;
            var discount = assignment.DiscountRequests
                .Where(request => request.Status == FeeDiscountStatus.Approved)
                .Sum(request => request.Amount);
            if (discount < 0 || discount > assignment.Amount)
            {
                throw new InvalidOperationException("An approved discount is greater than its assigned fee.");
            }

            db.FeeInvoices.Add(new FeeInvoice
            {
                StudentFeeAssignmentId = assignment.Id,
                StudentId = assignment.Enrollment!.StudentId,
                InvoiceNumber = NewDocumentNumber("INV-"),
                FeeName = structure.Name,
                Category = structure.Category,
                GrossAmount = assignment.Amount,
                DiscountAmount = discount,
                TotalAmount = assignment.Amount - discount,
                Currency = CurrencyCode,
                DueDate = assignment.DueDate,
                Status = FeeInvoiceStatus.Issued,
                IssuedAtUtc = DateTimeOffset.UtcNow
            });
            issued++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return issued;
    }

    public async Task RequestDiscountAsync(
        string actorUserId,
        DiscountRequestInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, SchoolRoles.Accountant, SchoolRoles.SuperAdmin);
        ValidateMoney(input.Amount, "Discount amount");
        var reason = RequiredText(input.Reason, "Discount reason", 500);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.StudentFeeAssignments
            .Include(item => item.DiscountRequests)
            .Include(item => item.Invoice)
            .SingleOrDefaultAsync(item => item.Id == input.StudentFeeAssignmentId, cancellationToken)
            ?? throw new KeyNotFoundException("The student fee assignment does not exist.");
        if (assignment.Invoice is not null) throw new InvalidOperationException("A discount must be approved before the invoice is issued.");
        if (input.Amount > assignment.Amount) throw new ArgumentOutOfRangeException(nameof(input.Amount), "A discount cannot exceed the assigned fee.");
        if (assignment.DiscountRequests.Any(request => request.Status is FeeDiscountStatus.PendingReview or FeeDiscountStatus.Approved))
        {
            throw new InvalidOperationException("This assignment already has a pending or approved discount request.");
        }

        db.FeeDiscountRequests.Add(new FeeDiscountRequest
        {
            StudentFeeAssignmentId = assignment.Id,
            Amount = input.Amount,
            Reason = reason,
            RequestedByUserId = actorUserId,
            RequestedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscountRequestSummary>> GetDiscountRequestsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceReaders);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var requests = await db.FeeDiscountRequests.AsNoTracking()
            .Include(item => item.StudentFeeAssignment)!.ThenInclude(assignment => assignment!.FeeStructure)
            .Include(item => item.StudentFeeAssignment)!.ThenInclude(assignment => assignment!.Enrollment)!.ThenInclude(enrollment => enrollment!.Student)
            .OrderByDescending(item => item.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        var reviewer = await userManager.FindByIdAsync(actorUserId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        var canReview = await userManager.IsInRoleAsync(reviewer, SchoolRoles.Principal)
            || await userManager.IsInRoleAsync(reviewer, SchoolRoles.SuperAdmin);
        var names = await GetUserNamesAsync(db,
            requests.SelectMany(item => new[] { item.RequestedByUserId, item.ReviewedByUserId })
                .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().ToArray(),
            cancellationToken);
        return requests.Select(item => new DiscountRequestSummary(
                item.Id,
                item.StudentFeeAssignmentId,
                item.StudentFeeAssignment?.Enrollment?.Student?.StudentNumber ?? "",
                item.StudentFeeAssignment?.Enrollment?.Student?.FullName ?? "Student",
                item.StudentFeeAssignment?.FeeStructure?.Name ?? "",
                item.StudentFeeAssignment?.Amount ?? 0,
                item.Amount,
                item.Reason,
                item.Status,
                names.GetValueOrDefault(item.RequestedByUserId, item.RequestedByUserId),
                item.RequestedAtUtc,
                item.ReviewedByUserId is null ? "" : names.GetValueOrDefault(item.ReviewedByUserId, item.ReviewedByUserId),
                item.ReviewedAtUtc,
                item.ReviewRemarks,
                item.Status == FeeDiscountStatus.PendingReview && canReview && item.RequestedByUserId != actorUserId))
            .ToArray();
    }

    public async Task ReviewDiscountAsync(
        string reviewerUserId,
        int requestId,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(reviewerUserId, FinanceReviewers);
        var cleanRemarks = OptionalText(remarks, 500) ?? "";
        if (!approve && string.IsNullOrWhiteSpace(cleanRemarks)) throw new ArgumentException("Add a reason when rejecting a discount request.", nameof(remarks));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await db.FeeDiscountRequests
            .Include(item => item.StudentFeeAssignment)!.ThenInclude(assignment => assignment!.Invoice)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("The discount request does not exist.");
        if (request.Status != FeeDiscountStatus.PendingReview) throw new InvalidOperationException("This discount request has already been reviewed.");
        if (request.RequestedByUserId == reviewerUserId) throw new UnauthorizedAccessException("A discount requester cannot approve or reject their own request.");
        if (request.StudentFeeAssignment?.Invoice is not null) throw new InvalidOperationException("The invoice was issued before this discount could be reviewed.");

        if (approve && await db.FeeDiscountRequests.AnyAsync(item => item.StudentFeeAssignmentId == request.StudentFeeAssignmentId
                && item.Id != request.Id && item.Status == FeeDiscountStatus.Approved,
                cancellationToken))
        {
            throw new InvalidOperationException("Another discount for this assignment is already approved.");
        }

        request.Status = approve ? FeeDiscountStatus.Approved : FeeDiscountStatus.Rejected;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedAtUtc = DateTimeOffset.UtcNow;
        request.ReviewRemarks = cleanRemarks;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<FinanceDashboard> GetDashboardAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        var setup = await GetSetupOptionsAsync(actorUserId, cancellationToken);
        var invoices = await GetAllInvoiceSummariesAsync(cancellationToken);
        var discounts = await GetDiscountRequestsAsync(actorUserId, cancellationToken);
        var refunds = await GetRefundRequestsAsync(actorUserId, cancellationToken);
        var reviewPayments = await GetGatewayReviewPaymentsAsync(cancellationToken);
        var overdueReminders = await GetOverdueRemindersAsync(actorUserId, cancellationToken);
        var report = await BuildFinancialReportAsync(invoices, cancellationToken);
        return new FinanceDashboard(
            setup,
            invoices,
            reviewPayments,
            overdueReminders,
            report,
            discounts.Count(item => item.Status == FeeDiscountStatus.PendingReview),
            refunds.Count(item => item.Status == RefundStatus.PendingReview),
            refunds);
    }

    public async Task<IReadOnlyList<OverdueReminderSummary>> GetOverdueRemindersAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        var overdue = (await GetAllInvoiceSummariesAsync(cancellationToken))
            .Where(invoice => invoice.IsOverdue && invoice.Balance > 0)
            .ToArray();
        if (overdue.Length == 0) return [];

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var studentIds = overdue.Select(invoice => invoice.StudentId).Distinct().ToArray();
        var links = await db.StudentGuardians.AsNoTracking()
            .Where(link => studentIds.Contains(link.StudentId))
            .Include(link => link.Guardian)
            .ToListAsync(cancellationToken);
        return overdue.Select(invoice => new OverdueReminderSummary(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.StudentId,
                invoice.StudentNumber,
                invoice.StudentName,
                invoice.FeeName,
                invoice.DueDate,
                invoice.Balance,
                links.Where(link => link.StudentId == invoice.StudentId && link.Guardian is not null)
                    .Select(link => new OverdueReminderContact(
                        link.Guardian!.FullName,
                        link.Guardian.Email,
                        link.Guardian.Phone))
                    .DistinctBy(contact => new { contact.Email, contact.Phone })
                    .ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<InvoiceSummary>> GetMyInvoicesAsync(
        string parentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(parentUserId, SchoolRoles.ParentStudent);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.FeeInvoices.AsNoTracking().Where(invoice =>
            db.StudentGuardians.Any(link => link.StudentId == invoice.StudentId
                && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == parentUserId)));
        return await MapInvoiceSummariesAsync(db, query, cancellationToken);
    }

    public async Task RecordManualPaymentAsync(
        string actorUserId,
        ManualPaymentInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        if (input.Method is not (PaymentMethod.Cash or PaymentMethod.BankTransfer))
        {
            throw new ArgumentException("Choose Cash or Bank transfer for an accountant-entered payment.", nameof(input.Method));
        }

        ValidateMoney(input.Amount, "Payment amount");
        var reference = OptionalText(input.Reference, 100);
        if (input.Method == PaymentMethod.BankTransfer && reference is null)
        {
            throw new ArgumentException("Add the bank transaction reference for a bank transfer.", nameof(input.Reference));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var invoice = await db.FeeInvoices.SingleOrDefaultAsync(item => item.Id == input.InvoiceId, cancellationToken)
            ?? throw new KeyNotFoundException("The invoice does not exist.");
        var balance = await GetInvoiceBalanceAsync(db, invoice, cancellationToken);
        if (invoice.Status != FeeInvoiceStatus.Issued || balance <= 0)
        {
            throw new InvalidOperationException("This invoice has no collectible balance.");
        }

        if (input.Amount > balance) throw new ArgumentOutOfRangeException(nameof(input.Amount), $"Payment cannot exceed the current balance of {balance:0.00} BDT.");
        if (input.Method == PaymentMethod.BankTransfer && await db.Payments.AnyAsync(item => item.Method == PaymentMethod.BankTransfer
                && item.Reference == reference && item.Status == PaymentStatus.Succeeded,
                cancellationToken))
        {
            throw new InvalidOperationException("This bank transaction reference has already been recorded.");
        }

        var now = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            FeeInvoiceId = invoice.Id,
            Amount = input.Amount,
            Currency = invoice.Currency,
            Method = input.Method,
            Status = PaymentStatus.Succeeded,
            Reference = reference,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now,
            PaidAtUtc = now
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        AddSuccessfulAllocationAndReceipt(db, payment, invoice.Id, input.Amount, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<StartOnlinePaymentResult> StartOnlinePaymentAsync(
        string parentUserId,
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(parentUserId, SchoolRoles.ParentStudent);
        if (!gateway.IsConfigured) throw new InvalidOperationException("SSLCOMMERZ sandbox credentials are not configured in local User Secrets.");
        var publicBaseUrl = configuration["Payments:SslCommerz:PublicBaseUrl"];
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Set an HTTPS public callback URL in Payments:SslCommerz:PublicBaseUrl before enabling online checkout.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.FeeInvoices.AsNoTracking()
            .Where(item => item.Id == invoiceId
                && item.Status == FeeInvoiceStatus.Issued
                && db.StudentGuardians.Any(link => link.StudentId == item.StudentId
                    && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == parentUserId)))
            .Include(item => item.Student)
            .Include(item => item.Payments).ThenInclude(payment => payment.Allocations)
            .Include(item => item.Payments).ThenInclude(payment => payment.RefundRequests)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("This invoice is unavailable to your parent account.");
        var received = invoice.Payments.Where(payment => payment.Status == PaymentStatus.Succeeded)
            .SelectMany(payment => payment.Allocations).Sum(allocation => allocation.Amount);
        var refunded = invoice.Payments.Where(payment => payment.Status == PaymentStatus.Succeeded)
            .SelectMany(payment => payment.RefundRequests)
            .Where(refund => refund.Status == RefundStatus.Completed)
            .Sum(refund => refund.Amount);
        var amountDue = Math.Max(invoice.TotalAmount - received + refunded, 0m);
        if (amountDue <= 0) throw new InvalidOperationException("This invoice has no balance to pay.");

        var linkedGuardian = await db.GuardianAccountLinks.AsNoTracking()
            .Where(account => account.UserId == parentUserId
                && db.StudentGuardians.Any(link => link.StudentId == invoice.StudentId && link.GuardianId == account.GuardianId))
            .Include(account => account.Guardian)
            .Select(account => new { account.Guardian!.FullName, account.Guardian.Email, account.Guardian.Phone })
            .FirstOrDefaultAsync(cancellationToken);
        var user = await userManager.FindByIdAsync(parentUserId)
            ?? throw new UnauthorizedAccessException("The signed-in parent account no longer exists.");
        var payerName = linkedGuardian?.FullName ?? invoice.Student?.FullName ?? "Parent";
        var payerEmail = linkedGuardian?.Email;
        if (string.IsNullOrWhiteSpace(payerEmail)) payerEmail = user.Email;
        if (string.IsNullOrWhiteSpace(payerEmail)) throw new InvalidOperationException("Add an email address to the linked guardian or parent account before online checkout.");
        var payerPhone = linkedGuardian?.Phone ?? user.PhoneNumber ?? "";
        if (string.IsNullOrWhiteSpace(payerPhone)) throw new InvalidOperationException("Add a phone number to the linked guardian or parent account before online checkout.");

        var transactionId = NewDocumentNumber("SM")[..30];
        var payment = new Payment
        {
            FeeInvoiceId = invoice.Id,
            Amount = amountDue,
            Currency = CurrencyCode,
            Method = PaymentMethod.OnlineGateway,
            Status = PaymentStatus.PendingGateway,
            GatewayName = "SSLCOMMERZ",
            GatewayTransactionId = transactionId,
            CreatedByUserId = parentUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var baseUrl = publicUri.AbsoluteUri.TrimEnd('/');
        var request = new GatewaySessionRequest(
            transactionId,
            amountDue,
            CurrencyCode,
            $"{baseUrl}/payments/sslcommerz/success",
            $"{baseUrl}/payments/sslcommerz/fail",
            $"{baseUrl}/payments/sslcommerz/cancel",
            $"{baseUrl}/payments/sslcommerz/ipn",
            payerName,
            payerEmail,
            payerPhone,
            $"School fee invoice {invoice.InvoiceNumber}");
        try
        {
            var session = await gateway.CreateSessionAsync(request, cancellationToken);
            await using var updateDb = await contextFactory.CreateDbContextAsync(cancellationToken);
            var pendingPayment = await updateDb.Payments.SingleAsync(item => item.Id == payment.Id, cancellationToken);
            if (pendingPayment.Status == PaymentStatus.PendingGateway)
            {
                pendingPayment.GatewaySessionKey = session.SessionKey;
                await updateDb.SaveChangesAsync(cancellationToken);
            }

            return new StartOnlinePaymentResult(session.GatewayPageUrl);
        }
        catch
        {
            await using var updateDb = await contextFactory.CreateDbContextAsync(cancellationToken);
            var pendingPayment = await updateDb.Payments.SingleOrDefaultAsync(item => item.Id == payment.Id, cancellationToken);
            if (pendingPayment?.Status == PaymentStatus.PendingGateway)
            {
                pendingPayment.Status = PaymentStatus.Failed;
                await updateDb.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task<GatewayPaymentOutcome> ProcessGatewayPaymentAsync(
        GatewayCallbackValues callback,
        CancellationToken cancellationToken = default)
    {
        var transactionId = RequiredText(callback.TransactionId, "Gateway transaction ID", 30);
        var validationId = RequiredText(callback.ValidationId, "Gateway validation ID", 50);
        await using var lookupDb = await contextFactory.CreateDbContextAsync(cancellationToken);
        var pending = await lookupDb.Payments.AsNoTracking()
            .Where(item => item.GatewayTransactionId == transactionId && item.Method == PaymentMethod.OnlineGateway)
            .Select(item => new { item.Id, item.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (pending is null) return GatewayPaymentOutcome.Ignored;
        if (pending.Status == PaymentStatus.Succeeded) return GatewayPaymentOutcome.AlreadyProcessed;
        if (pending.Status != PaymentStatus.PendingGateway && pending.Status != PaymentStatus.ReviewRequired)
        {
            return GatewayPaymentOutcome.Ignored;
        }

        var validation = await gateway.ValidatePaymentAsync(validationId, cancellationToken);
        if (!string.Equals(validation.ApiConnection, "DONE", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(validation.TransactionId, transactionId, StringComparison.Ordinal)
            || !(string.Equals(validation.Status, "VALID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(validation.Status, "VALIDATED", StringComparison.OrdinalIgnoreCase)))
        {
            return GatewayPaymentOutcome.Ignored;
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var payment = await db.Payments.Include(item => item.FeeInvoice)
            .SingleAsync(item => item.Id == pending.Id, cancellationToken);
        if (payment.Status == PaymentStatus.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return GatewayPaymentOutcome.AlreadyProcessed;
        }

        if (payment.Status != PaymentStatus.PendingGateway && payment.Status != PaymentStatus.ReviewRequired)
        {
            await transaction.CommitAsync(cancellationToken);
            return GatewayPaymentOutcome.Ignored;
        }

        var now = DateTimeOffset.UtcNow;
        var paymentMatches = validation.Amount == payment.Amount
            && validation.CurrencyAmount == payment.Amount
            && string.Equals(validation.CurrencyType, CurrencyCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(validation.CurrencyType, payment.Currency, StringComparison.OrdinalIgnoreCase)
            && string.Equals(validation.RiskLevel, "0", StringComparison.OrdinalIgnoreCase);
        var invoiceBalance = payment.FeeInvoice is null
            ? 0m
            : await GetInvoiceBalanceAsync(db, payment.FeeInvoice, cancellationToken);
        if (!paymentMatches || payment.FeeInvoice?.Status != FeeInvoiceStatus.Issued || invoiceBalance < payment.Amount)
        {
            payment.Status = PaymentStatus.ReviewRequired;
            payment.GatewayReceivedAmount = validation.Amount;
            payment.GatewayValidationId = validation.ValidationId;
            payment.GatewayBankTransactionId = validation.BankTransactionId;
            payment.GatewayRiskLevel = validation.RiskLevel;
            payment.PaidAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return GatewayPaymentOutcome.ReviewRequired;
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.GatewayReceivedAmount = validation.Amount;
        payment.GatewayValidationId = validation.ValidationId;
        payment.GatewayBankTransactionId = validation.BankTransactionId;
        payment.GatewayRiskLevel = validation.RiskLevel;
        payment.PaidAtUtc = now;
        AddSuccessfulAllocationAndReceipt(db, payment, payment.FeeInvoiceId, payment.Amount, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return GatewayPaymentOutcome.Processed;
    }

    public async Task<ReceiptDetails?> GetReceiptAsync(
        string actorUserId,
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, SchoolRoles.Accountant, SchoolRoles.SuperAdmin, SchoolRoles.ParentStudent);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await userManager.FindByIdAsync(actorUserId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        var isFinanceStaff = await userManager.IsInRoleAsync(user, SchoolRoles.Accountant)
            || await userManager.IsInRoleAsync(user, SchoolRoles.SuperAdmin);
        var query = db.Payments.AsNoTracking()
            .Where(item => item.Id == paymentId && item.Status == PaymentStatus.Succeeded);
        if (!isFinanceStaff)
        {
            query = query.Where(item => db.StudentGuardians.Any(link => link.StudentId == item.FeeInvoice!.StudentId
                && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == actorUserId)));
        }

        var payment = await query
            .Include(item => item.FeeInvoice)!.ThenInclude(invoice => invoice!.Student)
            .Include(item => item.FeeInvoice)!.ThenInclude(invoice => invoice!.StudentFeeAssignment)!.ThenInclude(assignment => assignment!.FeeStructure)!.ThenInclude(structure => structure!.AcademicYear)
            .Include(item => item.Receipt)
            .SingleOrDefaultAsync(cancellationToken);
        if (payment?.Receipt is null || payment.FeeInvoice is null) return null;
        return new ReceiptDetails(
            payment.Id,
            payment.Receipt.ReceiptNumber,
            payment.FeeInvoice.InvoiceNumber,
            payment.FeeInvoice.Student?.StudentNumber ?? "",
            payment.FeeInvoice.Student?.FullName ?? "Student",
            payment.FeeInvoice.FeeName,
            payment.FeeInvoice.StudentFeeAssignment?.FeeStructure?.AcademicYear?.Name ?? "",
            payment.Amount,
            payment.Currency,
            payment.Method,
            payment.Reference ?? payment.GatewayTransactionId ?? "",
            payment.PaidAtUtc ?? payment.CreatedAtUtc,
            payment.Receipt.IssuedAtUtc);
    }

    public async Task RequestRefundAsync(
        string actorUserId,
        RefundRequestInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        ValidateMoney(input.Amount, "Refund amount");
        var reason = RequiredText(input.Reason, "Refund reason", 500);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var payment = await db.Payments.Include(item => item.RefundRequests)
            .SingleOrDefaultAsync(item => item.Id == input.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException("The payment does not exist.");
        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.ReviewRequired))
        {
            throw new InvalidOperationException("Only a completed or held gateway payment can be refunded.");
        }

        var received = payment.Method == PaymentMethod.OnlineGateway
            ? payment.GatewayReceivedAmount ?? (payment.Status == PaymentStatus.Succeeded ? payment.Amount : 0m)
            : payment.Amount;
        var reserved = payment.RefundRequests
            .Where(item => item.Status is RefundStatus.PendingReview or RefundStatus.Approved or RefundStatus.Processing or RefundStatus.Completed)
            .Sum(item => item.Amount);
        if (input.Amount > received - reserved)
        {
            throw new ArgumentOutOfRangeException(nameof(input.Amount), $"Refunds cannot exceed the unrefunded received amount of {Math.Max(received - reserved, 0m):0.00} BDT.");
        }

        db.RefundRequests.Add(new RefundRequest
        {
            PaymentId = payment.Id,
            Amount = input.Amount,
            Reason = reason,
            RequestedByUserId = actorUserId,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            GatewayRefundTransactionId = payment.Method == PaymentMethod.OnlineGateway ? NewDocumentNumber("RF")[..30] : null
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefundRequestSummary>> GetRefundRequestsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceReaders);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var requests = await db.RefundRequests.AsNoTracking()
            .Include(item => item.Payment)!.ThenInclude(payment => payment!.FeeInvoice)!.ThenInclude(invoice => invoice!.Student)
            .OrderByDescending(item => item.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        var reviewer = await userManager.FindByIdAsync(actorUserId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        var canReview = await userManager.IsInRoleAsync(reviewer, SchoolRoles.Principal)
            || await userManager.IsInRoleAsync(reviewer, SchoolRoles.SuperAdmin);
        var names = await GetUserNamesAsync(db,
            requests.SelectMany(item => new[] { item.RequestedByUserId, item.ReviewedByUserId })
                .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().ToArray(),
            cancellationToken);
        return requests.Select(item => new RefundRequestSummary(
                item.Id,
                item.PaymentId,
                item.Payment?.FeeInvoiceId ?? 0,
                item.Payment?.FeeInvoice?.InvoiceNumber ?? "",
                item.Payment?.FeeInvoice?.Student?.FullName ?? "Student",
                item.Payment?.GatewayReceivedAmount ?? item.Payment?.Amount ?? 0,
                item.Amount,
                item.Reason,
                item.Status,
                item.Payment?.Method ?? PaymentMethod.Cash,
                names.GetValueOrDefault(item.RequestedByUserId, item.RequestedByUserId),
                item.RequestedAtUtc,
                item.ReviewedByUserId is null ? "" : names.GetValueOrDefault(item.ReviewedByUserId, item.ReviewedByUserId),
                item.ReviewedAtUtc,
                item.ReviewRemarks,
                item.GatewayRefundReference,
                item.GatewayRefundStatus,
                item.ManualRefundReference,
                item.Status == RefundStatus.PendingReview && canReview && item.RequestedByUserId != actorUserId,
                item.Payment?.Method == PaymentMethod.OnlineGateway
                    && !string.IsNullOrWhiteSpace(item.Payment.GatewayBankTransactionId)
                    && (item.Status == RefundStatus.Approved
                        || item.Status == RefundStatus.Processing && !string.IsNullOrWhiteSpace(item.GatewayRefundReference)),
                item.Status == RefundStatus.Approved
                    && (item.Payment?.Method != PaymentMethod.OnlineGateway || string.IsNullOrWhiteSpace(item.Payment.GatewayBankTransactionId))
                    || item.Status == RefundStatus.Processing
                        && item.GatewayRefundStatus == "UNKNOWN"
                        && string.IsNullOrWhiteSpace(item.GatewayRefundReference)))
            .ToArray();
    }

    public async Task ReviewRefundAsync(
        string reviewerUserId,
        int refundRequestId,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(reviewerUserId, FinanceReviewers);
        var cleanRemarks = OptionalText(remarks, 500) ?? "";
        if (!approve && string.IsNullOrWhiteSpace(cleanRemarks)) throw new ArgumentException("Add a reason when rejecting a refund request.", nameof(remarks));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await db.RefundRequests.SingleOrDefaultAsync(item => item.Id == refundRequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The refund request does not exist.");
        if (request.Status != RefundStatus.PendingReview) throw new InvalidOperationException("This refund request has already been reviewed.");
        if (request.RequestedByUserId == reviewerUserId) throw new UnauthorizedAccessException("A refund requester cannot approve or reject their own request.");
        request.Status = approve ? RefundStatus.Approved : RefundStatus.Rejected;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedAtUtc = DateTimeOffset.UtcNow;
        request.ReviewRemarks = cleanRemarks;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task StartOnlineRefundAsync(
        string actorUserId,
        int refundRequestId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        if (!gateway.IsConfigured) throw new InvalidOperationException("SSLCOMMERZ sandbox credentials are not configured in local User Secrets.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await db.RefundRequests
            .Include(item => item.Payment)
            .SingleOrDefaultAsync(item => item.Id == refundRequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The refund request does not exist.");
        if (request.Status != RefundStatus.Approved) throw new InvalidOperationException("Only an approved refund can be sent to the gateway.");
        if (request.Payment?.Method != PaymentMethod.OnlineGateway) throw new InvalidOperationException("This payment must be refunded through the accountant's manual refund record.");
        if (string.IsNullOrWhiteSpace(request.Payment.GatewayBankTransactionId))
        {
            throw new InvalidOperationException("The gateway did not provide a bank transaction ID. Record the external refund manually after reconciling it with the provider.");
        }

        var gatewayRequest = new GatewayRefundRequest(
            request.Payment.GatewayBankTransactionId,
            request.GatewayRefundTransactionId ?? throw new InvalidOperationException("The refund transaction reference is missing."),
            request.Amount,
            request.Reason,
            $"refund-{request.Id}");
        request.Status = RefundStatus.Processing;
        request.GatewayRefundStatus = "REQUESTING";
        request.ReviewRemarks = "";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        GatewayRefundStartResponse response;
        try
        {
            response = await gateway.StartRefundAsync(gatewayRequest, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            // The request may have reached the provider even when its response is lost; keep the amount reserved for reconciliation.
            await using var unknownDb = await contextFactory.CreateDbContextAsync(CancellationToken.None);
            var uncertain = await unknownDb.RefundRequests.SingleOrDefaultAsync(item => item.Id == refundRequestId, CancellationToken.None);
            if (uncertain?.Status == RefundStatus.Processing && string.IsNullOrWhiteSpace(uncertain.GatewayRefundReference))
            {
                uncertain.GatewayRefundStatus = "UNKNOWN";
                uncertain.ReviewRemarks = "The provider outcome could not be confirmed. Reconcile with SSLCOMMERZ before retrying or recording a manual completion.";
                await unknownDb.SaveChangesAsync(CancellationToken.None);
            }

            throw new InvalidOperationException("The SSLCOMMERZ refund outcome could not be confirmed. Reconcile it with the provider before taking another action.");
        }

        await using var updateDb = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await updateDb.RefundRequests.SingleAsync(item => item.Id == request.Id, cancellationToken);
        if (current.Status != RefundStatus.Processing || current.GatewayRefundStatus != "REQUESTING")
        {
            throw new InvalidOperationException("The refund request changed while contacting the gateway.");
        }
        current.GatewayRefundStatus = response.Status;
        if (!string.Equals(response.ApiConnection, "DONE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(response.RefundReference))
        {
            current.Status = RefundStatus.Failed;
            current.ReviewRemarks = OptionalText(response.ErrorReason, 500) ?? "The gateway did not accept the refund request.";
            await updateDb.SaveChangesAsync(cancellationToken);
            return;
        }

        current.GatewayRefundReference = response.RefundReference;
        current.Status = RefundStatus.Processing;
        await updateDb.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshOnlineRefundStatusAsync(
        string actorUserId,
        int refundRequestId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        if (!gateway.IsConfigured) throw new InvalidOperationException("SSLCOMMERZ sandbox credentials are not configured in local User Secrets.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var request = await db.RefundRequests.SingleOrDefaultAsync(item => item.Id == refundRequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The refund request does not exist.");
        if (request.Status != RefundStatus.Processing || string.IsNullOrWhiteSpace(request.GatewayRefundReference))
        {
            throw new InvalidOperationException("Only a gateway refund in processing state can be refreshed.");
        }

        var response = await gateway.GetRefundStatusAsync(request.GatewayRefundReference, cancellationToken);
        if (!string.Equals(response.ApiConnection, "DONE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The gateway could not confirm the refund status. Try again later.");
        }

        request.GatewayRefundStatus = response.Status;
        if (string.Equals(response.Status, "refunded", StringComparison.OrdinalIgnoreCase))
        {
            request.Status = RefundStatus.Completed;
            request.CompletedAtUtc = DateTimeOffset.UtcNow;
        }
        else if (string.Equals(response.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            request.Status = RefundStatus.Failed;
            request.ReviewRemarks = OptionalText(response.ErrorReason, 500) ?? "The gateway reported a failed refund.";
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteManualRefundAsync(
        string actorUserId,
        int refundRequestId,
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, FinanceStaff);
        var reference = RequiredText(externalReference, "Refund reference", 100);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await db.RefundRequests.Include(item => item.Payment)
            .SingleOrDefaultAsync(item => item.Id == refundRequestId, cancellationToken)
            ?? throw new KeyNotFoundException("The refund request does not exist.");
        var reconciledUnknownGatewayOutcome = request.Status == RefundStatus.Processing
            && request.GatewayRefundStatus == "UNKNOWN"
            && string.IsNullOrWhiteSpace(request.GatewayRefundReference)
            && request.Payment?.Method == PaymentMethod.OnlineGateway;
        if (request.Status != RefundStatus.Approved && !reconciledUnknownGatewayOutcome)
        {
            throw new InvalidOperationException("Only an approved refund or a provider outcome that needs reconciliation can be marked complete.");
        }

        if (request.Payment?.Method == PaymentMethod.OnlineGateway
            && !string.IsNullOrWhiteSpace(request.Payment.GatewayBankTransactionId)
            && !reconciledUnknownGatewayOutcome)
        {
            throw new InvalidOperationException("Use the SSLCOMMERZ refund flow for an online payment with a bank transaction ID.");
        }

        if (await db.RefundRequests.AnyAsync(item => item.Id != request.Id
                && item.ManualRefundReference == reference,
                cancellationToken))
        {
            throw new InvalidOperationException("This refund reference has already been recorded.");
        }

        request.Status = RefundStatus.Completed;
        request.ManualRefundReference = reference;
        request.CompletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InvoiceSummary>> GetAllInvoiceSummariesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await MapInvoiceSummariesAsync(db, db.FeeInvoices.AsNoTracking(), cancellationToken);
    }

    private static async Task<IReadOnlyList<InvoiceSummary>> MapInvoiceSummariesAsync(
        IApplicationDbContext db,
        IQueryable<FeeInvoice> query,
        CancellationToken cancellationToken)
    {
        var invoices = await query
            .Include(invoice => invoice.Student)
            .Include(invoice => invoice.StudentFeeAssignment)!.ThenInclude(assignment => assignment!.FeeStructure)!.ThenInclude(structure => structure!.AcademicYear)
            .Include(invoice => invoice.StudentFeeAssignment)!.ThenInclude(assignment => assignment!.Enrollment)!.ThenInclude(enrollment => enrollment!.Section)!.ThenInclude(section => section!.Class)
            .Include(invoice => invoice.Payments).ThenInclude(payment => payment.Allocations)
            .Include(invoice => invoice.Payments).ThenInclude(payment => payment.Receipt)
            .Include(invoice => invoice.Payments).ThenInclude(payment => payment.RefundRequests)
            .OrderBy(invoice => invoice.DueDate)
            .ThenBy(invoice => invoice.InvoiceNumber)
            .ToListAsync(cancellationToken);
        var timeZone = await GetSchoolTimeZoneAsync(db, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        return invoices.Select(invoice =>
            {
                var succeeded = invoice.Payments.Where(payment => payment.Status == PaymentStatus.Succeeded).ToArray();
                var received = succeeded.SelectMany(payment => payment.Allocations).Sum(allocation => allocation.Amount);
                var refunded = succeeded.SelectMany(payment => payment.RefundRequests)
                    .Where(refund => refund.Status == RefundStatus.Completed)
                    .Sum(refund => refund.Amount);
                var balance = invoice.Status == FeeInvoiceStatus.Issued
                    ? Math.Max(invoice.TotalAmount - received + refunded, 0m)
                    : 0m;
                var payments = invoice.Payments.OrderByDescending(payment => payment.CreatedAtUtc)
                    .Select(payment => new PaymentSummary(
                        payment.Id,
                        payment.GatewayReceivedAmount ?? payment.Amount,
                        payment.Method,
                        payment.Status,
                        payment.Reference ?? "",
                        payment.GatewayTransactionId ?? "",
                        payment.CreatedAtUtc,
                        payment.PaidAtUtc,
                        payment.Receipt?.ReceiptNumber ?? ""))
                    .ToArray();
                return new InvoiceSummary(
                    invoice.Id,
                    invoice.StudentId,
                    invoice.Student?.StudentNumber ?? "",
                    invoice.Student?.FullName ?? "Student",
                    invoice.StudentFeeAssignment?.FeeStructure?.AcademicYear?.Name ?? "",
                    invoice.StudentFeeAssignment?.Enrollment?.Section?.Class?.Name ?? "",
                    invoice.StudentFeeAssignment?.Enrollment?.Section?.Name ?? "",
                    invoice.InvoiceNumber,
                    invoice.FeeName,
                    invoice.Category,
                    invoice.GrossAmount,
                    invoice.DiscountAmount,
                    invoice.TotalAmount,
                    received,
                    refunded,
                    balance,
                    invoice.Currency,
                    invoice.DueDate,
                    invoice.Status,
                    invoice.Status == FeeInvoiceStatus.Issued && balance > 0 && invoice.DueDate < today,
                    invoice.IssuedAtUtc,
                    payments);
            })
            .ToArray();
    }

    private async Task<FinanceReport> BuildFinancialReportAsync(
        IReadOnlyList<InvoiceSummary> invoices,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var activeInvoices = invoices.Where(invoice => invoice.Status == FeeInvoiceStatus.Issued).ToArray();
        var since = DateTimeOffset.UtcNow.AddMonths(-11);
        var payments = await db.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Succeeded && payment.PaidAtUtc >= since)
            .Include(payment => payment.Allocations)
            .ToListAsync(cancellationToken);
        var timeZone = await GetSchoolTimeZoneAsync(db, cancellationToken);
        var monthly = payments
            .Where(payment => payment.PaidAtUtc.HasValue)
            .SelectMany(payment => payment.Allocations.Select(allocation => new
            {
                LocalTime = TimeZoneInfo.ConvertTime(payment.PaidAtUtc!.Value, timeZone),
                allocation.Amount
            }))
            .GroupBy(item => new { item.LocalTime.Year, item.LocalTime.Month })
            .OrderBy(item => item.Key.Year)
            .ThenBy(item => item.Key.Month)
            .Select(group => new MonthlyCollectionSummary(group.Key.Year, group.Key.Month, group.Sum(item => item.Amount)))
            .ToArray();
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return new FinanceReport(
            activeInvoices.Sum(invoice => invoice.TotalAmount),
            activeInvoices.Sum(invoice => invoice.DiscountAmount),
            activeInvoices.Sum(invoice => invoice.AmountReceived),
            activeInvoices.Sum(invoice => invoice.AmountRefunded),
            activeInvoices.Sum(invoice => invoice.Balance),
            activeInvoices.Where(invoice => invoice.IsOverdue).Sum(invoice => invoice.Balance),
            payments.Where(payment => payment.PaidAtUtc.HasValue
                    && TimeZoneInfo.ConvertTime(payment.PaidAtUtc.Value, timeZone).Year == now.Year
                    && TimeZoneInfo.ConvertTime(payment.PaidAtUtc.Value, timeZone).Month == now.Month)
                .SelectMany(payment => payment.Allocations).Sum(allocation => allocation.Amount),
            activeInvoices.Count(invoice => invoice.Balance > 0),
            activeInvoices.Count(invoice => invoice.IsOverdue),
            monthly);
    }

    private async Task<IReadOnlyList<GatewayPaymentReviewSummary>> GetGatewayReviewPaymentsAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking()
            .Where(payment => payment.Method == PaymentMethod.OnlineGateway && payment.Status == PaymentStatus.ReviewRequired)
            .Include(payment => payment.FeeInvoice)!.ThenInclude(invoice => invoice!.Student)
            .Include(payment => payment.RefundRequests)
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return payments
            .Where(payment => !(payment.GatewayReceivedAmount is > 0
                && payment.RefundRequests.Where(refund => refund.Status == RefundStatus.Completed).Sum(refund => refund.Amount) >= payment.GatewayReceivedAmount.Value))
            .Select(payment => new GatewayPaymentReviewSummary(
                payment.Id,
                payment.FeeInvoiceId,
                payment.FeeInvoice?.InvoiceNumber ?? "",
                payment.FeeInvoice?.Student?.FullName ?? "Student",
                payment.Amount,
                payment.GatewayReceivedAmount,
                payment.Currency,
                payment.GatewayRiskLevel == "1" ? "Gateway risk review required." : "Gateway amount, currency, or invoice balance needs reconciliation.",
                payment.CreatedAtUtc))
            .ToArray();
    }

    private static async Task<decimal> GetInvoiceBalanceAsync(
        IApplicationDbContext db,
        FeeInvoice invoice,
        CancellationToken cancellationToken)
    {
        var received = await db.PaymentAllocations
            .Where(allocation => allocation.FeeInvoiceId == invoice.Id && allocation.Payment!.Status == PaymentStatus.Succeeded)
            .SumAsync(allocation => (decimal?)allocation.Amount, cancellationToken) ?? 0m;
        var refunded = await db.RefundRequests
            .Where(request => request.Payment!.FeeInvoiceId == invoice.Id
                && request.Payment.Status == PaymentStatus.Succeeded
                && request.Status == RefundStatus.Completed)
            .SumAsync(request => (decimal?)request.Amount, cancellationToken) ?? 0m;
        return Math.Max(invoice.TotalAmount - received + refunded, 0m);
    }

    private static async Task<TimeZoneInfo> GetSchoolTimeZoneAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await db.SchoolProfiles.AsNoTracking()
            .OrderBy(profile => profile.Id)
            .Select(profile => profile.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static void AddSuccessfulAllocationAndReceipt(
        IApplicationDbContext db,
        Payment payment,
        int invoiceId,
        decimal amount,
        DateTimeOffset now)
    {
        db.PaymentAllocations.Add(new PaymentAllocation
        {
            Payment = payment,
            FeeInvoiceId = invoiceId,
            Amount = amount,
            AllocatedAtUtc = now
        });
        db.PaymentReceipts.Add(new PaymentReceipt
        {
            Payment = payment,
            ReceiptNumber = NewDocumentNumber("RCT-"),
            IssuedAtUtc = now
        });
    }

    private async Task<Dictionary<string, string>> GetUserNamesAsync(
        IApplicationDbContext db,
        string[] userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Length == 0) return new Dictionary<string, string>();
        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.FullName, cancellationToken);
        var emails = await userManager.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.Id, cancellationToken);
        return userIds.Distinct().ToDictionary(id => id, id => profiles.GetValueOrDefault(id, emails.GetValueOrDefault(id, id)));
    }

    private async Task EnsureRoleAsync(string userId, params string[] allowedRoles)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("A signed-in school account is required.");
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        foreach (var role in allowedRoles)
        {
            if (await userManager.IsInRoleAsync(user, role)) return;
        }

        throw new UnauthorizedAccessException("Your school account is not allowed to perform this finance action.");
    }

    private static void ValidateMoney(decimal amount, string label)
    {
        if (amount <= 0 || amount > 9_999_999_999.99m || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"{label} must be positive and use at most two decimal places.");
        }
    }

    private static string NewDocumentNumber(string prefix) => $"{prefix}{Guid.NewGuid():N}";

    private static string RequiredText(string? value, string label, int maximumLength)
    {
        var text = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength)
        {
            throw new ArgumentException($"{label} is required and must be {maximumLength} characters or fewer.");
        }

        return text;
    }

    private static string? OptionalText(string? value, int maximumLength)
    {
        var text = value?.Trim();
        if (text?.Length > maximumLength) throw new ArgumentException($"Text must be {maximumLength} characters or fewer.");
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
