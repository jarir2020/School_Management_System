using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class ResultApproval
{
    public int Id { get; set; }

    public int ExaminationId { get; set; }

    public ResultApprovalDecision Decision { get; set; }

    [Required, StringLength(450)]
    public string ReviewerUserId { get; set; } = "";

    [StringLength(1000)]
    public string Remarks { get; set; } = "";

    public DateTimeOffset ReviewedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Examination? Examination { get; set; }
}
