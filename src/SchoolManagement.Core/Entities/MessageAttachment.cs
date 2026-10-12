using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class MessageAttachment
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    [Required, StringLength(180)]
    public string OriginalFileName { get; set; } = "";

    [Required, StringLength(100)]
    public string ContentType { get; set; } = "";

    [Required, StringLength(80)]
    public string StorageKey { get; set; } = "";

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SchoolMessage? Message { get; set; }
}
