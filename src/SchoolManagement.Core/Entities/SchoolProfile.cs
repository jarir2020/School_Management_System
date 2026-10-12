using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

/// <summary>
/// Stores the single school's contact and locale settings for the initial version.
/// </summary>
public sealed class SchoolProfile
{
    public const int PrimaryProfileId = 1;

    public int Id { get; set; } = PrimaryProfileId;

    [Required, StringLength(160)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string Address { get; set; } = "";

    [Phone, StringLength(40)]
    public string Phone { get; set; } = "";

    [EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    // UTC is a safe starting point; a school administrator can configure the local zone.
    [Required, StringLength(100)]
    public string TimeZoneId { get; set; } = "UTC";

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
