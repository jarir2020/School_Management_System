using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class AttendanceCorrectionRequest
{
    public int Id { get; set; }

    public int AttendanceRecordId { get; set; }

    public AttendanceStatus PreviousStatus { get; set; }

    public AttendanceStatus RequestedStatus { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = "";

    [Required, StringLength(450)]
    public string RequestedByUserId { get; set; } = "";

    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public CorrectionRequestStatus Status { get; set; } = CorrectionRequestStatus.Pending;

    public string? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    [StringLength(500)]
    public string? ReviewerRemarks { get; set; }

    public AttendanceRecord? AttendanceRecord { get; set; }
}
