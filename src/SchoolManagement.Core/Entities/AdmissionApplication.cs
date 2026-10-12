using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class AdmissionApplication
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string ApplicationNumber { get; set; } = "";

    public int AcademicYearId { get; set; }

    public int? RequestedSectionId { get; set; }

    [Required, StringLength(160)]
    public string StudentFullName { get; set; } = "";

    public DateOnly DateOfBirth { get; set; }

    [StringLength(40)]
    public string Gender { get; set; } = "";

    [StringLength(500)]
    public string StudentAddress { get; set; } = "";

    [Required, StringLength(160)]
    public string GuardianFullName { get; set; } = "";

    [Required, StringLength(60)]
    public string GuardianRelationship { get; set; } = "";

    [Required, StringLength(40)]
    public string GuardianPhone { get; set; } = "";

    [StringLength(254)]
    public string GuardianEmail { get; set; } = "";

    [StringLength(500)]
    public string GuardianAddress { get; set; } = "";

    public AdmissionStatus Status { get; set; } = AdmissionStatus.PendingReview;

    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AcademicYear? AcademicYear { get; set; }

    public Section? RequestedSection { get; set; }

    public Student? Student { get; set; }

    public ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();

    public ICollection<AdmissionReview> Reviews { get; set; } = new List<AdmissionReview>();
}
