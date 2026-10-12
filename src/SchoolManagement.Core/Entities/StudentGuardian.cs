namespace SchoolManagement.Core.Entities;

public sealed class StudentGuardian
{
    public int StudentId { get; set; }

    public int GuardianId { get; set; }

    public Student? Student { get; set; }

    public Guardian? Guardian { get; set; }
}
