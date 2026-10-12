using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class EmailOutboxMessage
{
    public long Id { get; set; }

    [Required, StringLength(320)]
    public string Recipient { get; set; } = "";

    [Required, StringLength(180)]
    public string Subject { get; set; } = "";

    [Required, StringLength(12000)]
    public string HtmlBody { get; set; } = "";

    [Required, StringLength(220)]
    public string DeduplicationKey { get; set; } = "";

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset NextAttemptAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAtUtc { get; set; }

    [StringLength(500)]
    public string LastError { get; set; } = "";
}
