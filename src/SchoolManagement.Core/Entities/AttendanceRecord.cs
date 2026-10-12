using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class AttendanceRecord
{
    public int Id { get; set; }

    public int AttendanceSessionId { get; set; }

    public int StudentId { get; set; }

    public AttendanceStatus Status { get; set; }

    public string Note { get; set; } = "";

    public AttendanceSession? AttendanceSession { get; set; }

    public Student? Student { get; set; }
}
