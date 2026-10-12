using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class ApplicationDocument
{
    public int Id { get; set; }

    public int AdmissionApplicationId { get; set; }

    public AdmissionDocumentType Type { get; set; }

    [Required, StringLength(180)]
    public string OriginalFileName { get; set; } = "";

    [Required, StringLength(100)]
    public string ContentType { get; set; } = "";

    [Required, StringLength(80)]
    public string StorageKey { get; set; } = "";

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AdmissionApplication? AdmissionApplication { get; set; }
}
