using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class FeeDiscountRequest
{
    public int Id { get; set; }

    public int StudentFeeAssignmentId { get; set; }

    public decimal Amount { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = "";

    public FeeDiscountStatus Status { get; set; } = FeeDiscountStatus.PendingReview;

    [Required, StringLength(450)]
    public string RequestedByUserId { get; set; } = "";

    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(450)]
    public string? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    [StringLength(500)]
    public string ReviewRemarks { get; set; } = "";

    public StudentFeeAssignment? StudentFeeAssignment { get; set; }
}
