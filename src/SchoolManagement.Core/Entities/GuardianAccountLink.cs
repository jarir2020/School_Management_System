using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class GuardianAccountLink
{
    public int GuardianId { get; set; }

    [Required, StringLength(450)]
    public string UserId { get; set; } = "";

    [Required, StringLength(450)]
    public string LinkedByUserId { get; set; } = "";

    public DateTimeOffset LinkedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guardian? Guardian { get; set; }
}
