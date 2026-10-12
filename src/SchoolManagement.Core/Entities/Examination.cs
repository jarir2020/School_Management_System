using System.ComponentModel.DataAnnotations;
using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class Examination
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }

    public int TermId { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    public ExaminationStatus Status { get; set; } = ExaminationStatus.Draft;

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AcademicYear? AcademicYear { get; set; }

    public Term? Term { get; set; }

    public ICollection<ExamSubject> Subjects { get; set; } = new List<ExamSubject>();

    public ICollection<ResultApproval> Approvals { get; set; } = new List<ResultApproval>();
}
