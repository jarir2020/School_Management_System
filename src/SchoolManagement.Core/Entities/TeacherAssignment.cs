using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class TeacherAssignment
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string TeacherUserId { get; set; } = "";

    public int SectionId { get; set; }

    public int SubjectId { get; set; }

    public int AcademicYearId { get; set; }

    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? AssignedByUserId { get; set; }

    public Section? Section { get; set; }

    public Subject? Subject { get; set; }

    public AcademicYear? AcademicYear { get; set; }
}
