using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Core.Entities;

public sealed class StudentMark
{
    public int Id { get; set; }

    public int ExamSubjectId { get; set; }

    public int StudentId { get; set; }

    public decimal? MarksObtained { get; set; }

    [Required, StringLength(450)]
    public string EnteredByUserId { get; set; } = "";

    [Required, StringLength(450)]
    public string UpdatedByUserId { get; set; } = "";

    public DateTimeOffset EnteredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ExamSubject? ExamSubject { get; set; }

    public Student? Student { get; set; }

    public ICollection<ResultRevision> Revisions { get; set; } = new List<ResultRevision>();
}
