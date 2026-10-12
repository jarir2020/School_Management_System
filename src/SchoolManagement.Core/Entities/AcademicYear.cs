using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class AcademicYear
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string Name { get; set; } = "";

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }
}
