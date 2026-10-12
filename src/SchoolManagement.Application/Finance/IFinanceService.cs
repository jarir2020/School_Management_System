namespace SchoolManagement.Application.Finance;

public interface IFinanceService
{
    Task<FinanceSetupOptions> GetSetupOptionsAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task CreateFeeStructureAsync(string actorUserId, FeeStructureInput input, CancellationToken cancellationToken = default);

    Task<int> AssignFeeStructureAsync(string actorUserId, int feeStructureId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeAssignmentSummary>> GetAssignmentsAsync(string actorUserId, int feeStructureId, CancellationToken cancellationToken = default);

    Task<int> IssueInvoicesAsync(string actorUserId, int feeStructureId, CancellationToken cancellationToken = default);

    Task RequestDiscountAsync(string actorUserId, DiscountRequestInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscountRequestSummary>> GetDiscountRequestsAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task ReviewDiscountAsync(string reviewerUserId, int requestId, bool approve, string? remarks, CancellationToken cancellationToken = default);

    Task<FinanceDashboard> GetDashboardAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OverdueReminderSummary>> GetOverdueRemindersAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceSummary>> GetMyInvoicesAsync(string parentUserId, CancellationToken cancellationToken = default);

    Task RecordManualPaymentAsync(string actorUserId, ManualPaymentInput input, CancellationToken cancellationToken = default);

    Task<StartOnlinePaymentResult> StartOnlinePaymentAsync(string parentUserId, int invoiceId, CancellationToken cancellationToken = default);

    Task<GatewayPaymentOutcome> ProcessGatewayPaymentAsync(GatewayCallbackValues callback, CancellationToken cancellationToken = default);

    Task<ReceiptDetails?> GetReceiptAsync(string actorUserId, int paymentId, CancellationToken cancellationToken = default);

    Task RequestRefundAsync(string actorUserId, RefundRequestInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundRequestSummary>> GetRefundRequestsAsync(string actorUserId, CancellationToken cancellationToken = default);

    Task ReviewRefundAsync(string reviewerUserId, int refundRequestId, bool approve, string? remarks, CancellationToken cancellationToken = default);

    Task StartOnlineRefundAsync(string actorUserId, int refundRequestId, CancellationToken cancellationToken = default);

    Task RefreshOnlineRefundStatusAsync(string actorUserId, int refundRequestId, CancellationToken cancellationToken = default);

    Task CompleteManualRefundAsync(string actorUserId, int refundRequestId, string externalReference, CancellationToken cancellationToken = default);
}
