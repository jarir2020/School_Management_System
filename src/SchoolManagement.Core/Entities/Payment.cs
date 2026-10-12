using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class Payment
{
    public int Id { get; set; }

    public int FeeInvoiceId { get; set; }

    public decimal Amount { get; set; }

    public decimal? GatewayReceivedAmount { get; set; }

    [Required, StringLength(3)]
    public string Currency { get; set; } = "BDT";

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(30)]
    public string GatewayName { get; set; } = "";

    [StringLength(30)]
    public string? GatewayTransactionId { get; set; }

    [StringLength(80)]
    public string GatewaySessionKey { get; set; } = "";

    [StringLength(50)]
    public string? GatewayValidationId { get; set; }

    [StringLength(80)]
    public string GatewayBankTransactionId { get; set; } = "";

    [StringLength(4)]
    public string GatewayRiskLevel { get; set; } = "";

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PaidAtUtc { get; set; }

    public FeeInvoice? FeeInvoice { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();

    public PaymentReceipt? Receipt { get; set; }

    public ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();
}
