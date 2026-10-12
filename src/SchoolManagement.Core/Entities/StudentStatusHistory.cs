using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class StudentStatusHistory
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public StudentStatus? PreviousStatus { get; set; }

    public StudentStatus NewStatus { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ChangedByUserId { get; set; } = "";

    public string Reason { get; set; } = "";

    public Student? Student { get; set; }
}
