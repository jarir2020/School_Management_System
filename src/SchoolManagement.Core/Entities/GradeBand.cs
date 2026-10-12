using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class GradeBand
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }

    public decimal MinimumPercentage { get; set; }

    [Required, StringLength(8)]
    public string LetterGrade { get; set; } = "";

    public decimal GradePoint { get; set; }

    public bool IsPassing { get; set; }

    [StringLength(120)]
    public string Remark { get; set; } = "";

    public AcademicYear? AcademicYear { get; set; }
}
