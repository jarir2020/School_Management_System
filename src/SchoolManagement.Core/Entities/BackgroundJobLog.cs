using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class BackgroundJobLog
{
    public long Id { get; set; }

    [Required, StringLength(80)]
    public string JobType { get; set; } = "";

    [Required, StringLength(20)]
    public string Status { get; set; } = "";

    [StringLength(500)]
    public string Details { get; set; } = "";

    public int AttemptCount { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
