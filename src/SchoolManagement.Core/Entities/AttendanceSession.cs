using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class AttendanceSession
{
    public int Id { get; set; }

    public int SectionId { get; set; }

    public int SubjectId { get; set; }

    public int AcademicYearId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    [Required, StringLength(450)]
    public string TeacherUserId { get; set; } = "";

    public DateTimeOffset EnteredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Section? Section { get; set; }

    public Subject? Subject { get; set; }

    public AcademicYear? AcademicYear { get; set; }

    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
