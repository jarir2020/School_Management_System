using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class Enrollment
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int AcademicYearId { get; set; }

    public int SectionId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public DateTimeOffset EnrolledAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Student? Student { get; set; }

    public AcademicYear? AcademicYear { get; set; }

    public Section? Section { get; set; }
}
