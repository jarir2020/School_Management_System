using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class ResultRevision
{
    public int Id { get; set; }

    public int StudentMarkId { get; set; }

    public decimal PreviousMarks { get; set; }

    public decimal NewMarks { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = "";

    [Required, StringLength(450)]
    public string RevisedByUserId { get; set; } = "";

    public DateTimeOffset RevisedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public StudentMark? StudentMark { get; set; }
}
