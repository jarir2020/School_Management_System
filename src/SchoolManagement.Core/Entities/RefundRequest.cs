using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class RefundRequest
{
    public int Id { get; set; }

    public int PaymentId { get; set; }

    public decimal Amount { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = "";

    public RefundStatus Status { get; set; } = RefundStatus.PendingReview;

    [Required, StringLength(450)]
    public string RequestedByUserId { get; set; } = "";

    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(450)]
    public string? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    [StringLength(500)]
    public string ReviewRemarks { get; set; } = "";

    [StringLength(30)]
    public string? GatewayRefundTransactionId { get; set; }

    [StringLength(50)]
    public string GatewayRefundReference { get; set; } = "";

    [StringLength(30)]
    public string GatewayRefundStatus { get; set; } = "";

    [StringLength(100)]
    public string ManualRefundReference { get; set; } = "";

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public Payment? Payment { get; set; }
}
