using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

/// <summary>Append-only application activity. Payloads must not contain credentials or student details.</summary>
public sealed class AuditLog
{
    public long Id { get; set; }

    [Required, StringLength(450)]
    public string ActorUserId { get; set; } = "";

    [Required, StringLength(80)]
    public string Action { get; set; } = "";

    [Required, StringLength(80)]
    public string EntityType { get; set; } = "";

    [StringLength(100)]
    public string EntityId { get; set; } = "";

    [StringLength(500)]
    public string Details { get; set; } = "";

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
