using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class Student
{
    public int Id { get; set; }

    public int AdmissionApplicationId { get; set; }

    [Required, StringLength(32)]
    public string StudentNumber { get; set; } = "";

    [Required, StringLength(160)]
    public string FullName { get; set; } = "";

    public DateOnly DateOfBirth { get; set; }

    [StringLength(40)]
    public string Gender { get; set; } = "";

    [StringLength(500)]
    public string Address { get; set; } = "";

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public DateTimeOffset AdmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AdmissionApplication? AdmissionApplication { get; set; }

    public ICollection<StudentGuardian> Guardians { get; set; } = new List<StudentGuardian>();

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public ICollection<StudentStatusHistory> StatusHistory { get; set; } = new List<StudentStatusHistory>();
}
