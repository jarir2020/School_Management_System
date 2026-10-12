using SchoolManagement.Core.Enums;

namespace SchoolManagement.Core.Entities;

public sealed class ExamSubject
{
    public int Id { get; set; }

    public int ExaminationId { get; set; }

    public int SectionId { get; set; }

    public int SubjectId { get; set; }

    public decimal FullMarks { get; set; }

    public decimal PassMarks { get; set; }

    public ExamSubjectStatus Status { get; set; } = ExamSubjectStatus.Configured;

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public Examination? Examination { get; set; }

    public Section? Section { get; set; }

    public Subject? Subject { get; set; }

    public ICollection<StudentMark> Marks { get; set; } = new List<StudentMark>();
}
