using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class AdmissionReview
{
    public int Id { get; set; }

    public int AdmissionApplicationId { get; set; }

    [Required, StringLength(450)]
    public string ReviewerUserId { get; set; } = "";

    public AdmissionDecision Decision { get; set; }

    [StringLength(1000)]
    public string Remarks { get; set; } = "";

    public DateTimeOffset ReviewedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AdmissionApplication? AdmissionApplication { get; set; }
}
