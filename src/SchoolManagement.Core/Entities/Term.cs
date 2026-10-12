using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class Term
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = "";

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public AcademicYear? AcademicYear { get; set; }
}
