using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Finance;

public sealed record FinanceYearOption(int Id, string Name);

public sealed record FinanceClassOption(int Id, string Name, int GradeLevel);

public sealed record FeeStructureSummary(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    int SchoolClassId,
    string ClassName,
    FeeCategory Category,
    string Name,
    decimal Amount,
    DateOnly DueDate,
    bool IsActive,
    int AssignedStudentCount,
    int UninvoicedAssignmentCount);

public sealed record FinanceSetupOptions(
    IReadOnlyList<FinanceYearOption> AcademicYears,
    IReadOnlyList<FinanceClassOption> Classes,
    IReadOnlyList<FeeStructureSummary> FeeStructures);

public sealed record FeeStructureInput(
    int AcademicYearId,
    int SchoolClassId,
    FeeCategory Category,
    string Name,
    decimal Amount,
    DateOnly DueDate);

public sealed record FeeAssignmentSummary(
    int Id,
    int StudentId,
    string StudentNumber,
    string StudentName,
    string ClassName,
    string SectionName,
    string FeeName,
    decimal Amount,
    DateOnly DueDate,
    decimal ApprovedDiscountAmount,
    int PendingDiscountCount,
    int? InvoiceId,
    string InvoiceNumber);

public sealed record DiscountRequestInput(int StudentFeeAssignmentId, decimal Amount, string Reason);

public sealed record DiscountRequestSummary(
    int Id,
    int StudentFeeAssignmentId,
    string StudentNumber,
    string StudentName,
    string FeeName,
    decimal FeeAmount,
    decimal DiscountAmount,
    string Reason,
    FeeDiscountStatus Status,
    string RequestedByName,
    DateTimeOffset RequestedAtUtc,
    string ReviewedByName,
    DateTimeOffset? ReviewedAtUtc,
    string ReviewRemarks,
    bool CanCurrentUserReview);

public sealed record InvoiceSummary(
    int Id,
    int StudentId,
    string StudentNumber,
    string StudentName,
    string AcademicYearName,
    string ClassName,
    string SectionName,
    string InvoiceNumber,
    string FeeName,
    FeeCategory Category,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal AmountReceived,
    decimal AmountRefunded,
    decimal Balance,
    string Currency,
    DateOnly DueDate,
    FeeInvoiceStatus Status,
    bool IsOverdue,
    DateTimeOffset IssuedAtUtc,
    IReadOnlyList<PaymentSummary> Payments);

public sealed record PaymentSummary(
    int Id,
    decimal Amount,
    PaymentMethod Method,
    PaymentStatus Status,
    string Reference,
    string GatewayTransactionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc,
    string ReceiptNumber);

public sealed record FinanceReport(
    decimal TotalInvoiced,
    decimal TotalDiscounts,
    decimal TotalReceived,
    decimal TotalRefunded,
    decimal TotalOutstanding,
    decimal TotalOverdue,
    decimal CurrentMonthCollection,
    int OpenInvoiceCount,
    int OverdueInvoiceCount,
    IReadOnlyList<MonthlyCollectionSummary> MonthlyCollections);

public sealed record MonthlyCollectionSummary(int Year, int Month, decimal Amount);

public sealed record GatewayPaymentReviewSummary(
    int PaymentId,
    int InvoiceId,
    string InvoiceNumber,
    string StudentName,
    decimal ExpectedAmount,
    decimal? ReceivedAmount,
    string Currency,
    string Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record OverdueReminderContact(string Name, string Email, string Phone);

public sealed record OverdueReminderSummary(
    int InvoiceId,
    string InvoiceNumber,
    int StudentId,
    string StudentNumber,
    string StudentName,
    string FeeName,
    DateOnly DueDate,
    decimal Balance,
    IReadOnlyList<OverdueReminderContact> Contacts);

public sealed record FinanceDashboard(
    FinanceSetupOptions Setup,
    IReadOnlyList<InvoiceSummary> Invoices,
    IReadOnlyList<GatewayPaymentReviewSummary> GatewayPaymentsNeedingReview,
    IReadOnlyList<OverdueReminderSummary> OverdueReminders,
    FinanceReport Report,
    int PendingDiscountCount,
    int PendingRefundCount,
    IReadOnlyList<RefundRequestSummary> RefundRequests);

public sealed record ManualPaymentInput(
    int InvoiceId,
    decimal Amount,
    PaymentMethod Method,
    string Reference);

public sealed record StartOnlinePaymentResult(string GatewayPageUrl);

public enum GatewayPaymentOutcome
{
    Processed,
    AlreadyProcessed,
    ReviewRequired,
    Ignored
}

public sealed record RefundRequestInput(int PaymentId, decimal Amount, string Reason);

public sealed record RefundRequestSummary(
    int Id,
    int PaymentId,
    int InvoiceId,
    string InvoiceNumber,
    string StudentName,
    decimal PaymentAmount,
    decimal RefundAmount,
    string Reason,
    RefundStatus Status,
    PaymentMethod PaymentMethod,
    string RequestedByName,
    DateTimeOffset RequestedAtUtc,
    string ReviewedByName,
    DateTimeOffset? ReviewedAtUtc,
    string ReviewRemarks,
    string GatewayRefundReference,
    string GatewayRefundStatus,
    string ManualRefundReference,
    bool CanCurrentUserReview,
    bool CanRefundThroughGateway,
    bool CanCompleteManualRefund);

public sealed record ReceiptDetails(
    int PaymentId,
    string ReceiptNumber,
    string InvoiceNumber,
    string StudentNumber,
    string StudentName,
    string FeeName,
    string AcademicYearName,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string Reference,
    DateTimeOffset PaidAtUtc,
    DateTimeOffset IssuedAtUtc);

public sealed record GatewayCallbackValues(string TransactionId, string ValidationId);
