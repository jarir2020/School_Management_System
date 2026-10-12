namespace SchoolManagement.Application.Finance;

public interface ISslCommerzGateway
{
    bool IsConfigured { get; }

    Task<GatewaySessionResponse> CreateSessionAsync(GatewaySessionRequest request, CancellationToken cancellationToken = default);

    Task<ValidatedGatewayPayment> ValidatePaymentAsync(string validationId, CancellationToken cancellationToken = default);

    Task<GatewayRefundStartResponse> StartRefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken = default);

    Task<GatewayRefundStatusResponse> GetRefundStatusAsync(string refundReference, CancellationToken cancellationToken = default);
}

public sealed record GatewaySessionRequest(
    string TransactionId,
    decimal Amount,
    string Currency,
    string SuccessUrl,
    string FailUrl,
    string CancelUrl,
    string IpNUrl,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string Description);

public sealed record GatewaySessionResponse(string GatewayPageUrl, string SessionKey);

public sealed record ValidatedGatewayPayment(
    string Status,
    string ApiConnection,
    string TransactionId,
    string ValidationId,
    decimal Amount,
    string CurrencyType,
    decimal CurrencyAmount,
    string BankTransactionId,
    string RiskLevel);

public sealed record GatewayRefundRequest(
    string BankTransactionId,
    string RefundTransactionId,
    decimal Amount,
    string Reason,
    string MerchantReference);

public sealed record GatewayRefundStartResponse(string ApiConnection, string Status, string RefundReference, string ErrorReason);

public sealed record GatewayRefundStatusResponse(string ApiConnection, string Status, string RefundReference, string ErrorReason);
