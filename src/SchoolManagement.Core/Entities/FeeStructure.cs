using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class FeeStructure
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }

    public int SchoolClassId { get; set; }

    public FeeCategory Category { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    public bool IsActive { get; set; } = true;

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AcademicYear? AcademicYear { get; set; }

    public SchoolClass? SchoolClass { get; set; }

    public ICollection<StudentFeeAssignment> Assignments { get; set; } = new List<StudentFeeAssignment>();
}
