using SchoolManagement.Core.Enums;

namespace SchoolManagement.Application.Examinations;

public sealed record TermSummary(int Id, int AcademicYearId, string AcademicYearName, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record ExaminationSummary(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    int TermId,
    string TermName,
    string Name,
    ExaminationStatus Status,
    DateTimeOffset CreatedAtUtc,
    int SubjectCount,
    int SubmittedSubjectCount,
    IReadOnlyList<ExamSubjectSummary> Subjects);

public sealed record ExamSubjectSummary(
    int Id,
    int SectionId,
    string ClassName,
    string SectionName,
    int SubjectId,
    string SubjectName,
    string SubjectCode,
    decimal FullMarks,
    decimal PassMarks,
    ExamSubjectStatus Status);

public sealed record ExamYearOption(int Id, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record ExamSectionOption(int Id, string ClassName, string Name);

public sealed record ExamSubjectOption(int Id, string Name, string Code, int SuggestedFullMarks);

public sealed record GradeBandSummary(
    int Id,
    int AcademicYearId,
    string AcademicYearName,
    decimal MinimumPercentage,
    string LetterGrade,
    decimal GradePoint,
    bool IsPassing,
    string Remark);

public sealed record ExaminationSetupOptions(
    IReadOnlyList<ExamYearOption> AcademicYears,
    IReadOnlyList<TermSummary> Terms,
    IReadOnlyList<ExamSectionOption> Sections,
    IReadOnlyList<ExamSubjectOption> Subjects,
    IReadOnlyList<ExaminationSummary> Examinations,
    IReadOnlyList<GradeBandSummary> GradeBands);

public sealed record TermInput(int AcademicYearId, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record ExaminationInput(int AcademicYearId, int TermId, string Name);

public sealed record ExamSubjectInput(int ExaminationId, int SectionId, int SubjectId, decimal FullMarks, decimal PassMarks);

public sealed record GradeBandInput(int AcademicYearId, decimal MinimumPercentage, string LetterGrade, decimal GradePoint, bool IsPassing, string? Remark);

public sealed record ExamWorkItem(
    int ExamSubjectId,
    int ExaminationId,
    string ExaminationName,
    ExaminationStatus ExaminationStatus,
    int AcademicYearId,
    string AcademicYearName,
    string TermName,
    int SectionId,
    string ClassName,
    string SectionName,
    int SubjectId,
    string SubjectName,
    string SubjectCode,
    decimal FullMarks,
    decimal PassMarks,
    ExamSubjectStatus Status);

public sealed record StudentMarkEntry(int StudentId, string StudentNumber, string StudentName, decimal? MarksObtained);

public sealed record ExamMarkRegister(ExamWorkItem WorkItem, IReadOnlyList<StudentMarkEntry> Students);

public sealed record MarkInput(int StudentId, decimal? MarksObtained);

public sealed record ExamReviewSummary(
    int ExaminationId,
    string ExaminationName,
    string AcademicYearName,
    string TermName,
    ExaminationStatus Status,
    int SubjectCount,
    int MarkCount,
    DateTimeOffset SubmittedAtUtc,
    IReadOnlyList<ExamReviewSubjectSummary> Subjects,
    IReadOnlyList<ResultApprovalSummary> Approvals);

public sealed record ExamReviewSubjectSummary(
    string ClassName,
    string SectionName,
    string SubjectName,
    decimal FullMarks,
    decimal PassMarks,
    IReadOnlyList<ExamReviewMark> Marks);

public sealed record ExamReviewMark(string StudentNumber, string StudentName, decimal MarksObtained);

public sealed record ResultApprovalSummary(
    ResultApprovalDecision Decision,
    string ReviewerName,
    string Remarks,
    DateTimeOffset ReviewedAtUtc);

public sealed record CalculatedGrade(string LetterGrade, decimal GradePoint, bool IsPassing, decimal Percentage, string Remark);

public sealed record ReportCardSubject(
    string ClassName,
    string SectionName,
    string SubjectName,
    decimal MarksObtained,
    decimal FullMarks,
    decimal PassMarks,
    CalculatedGrade Grade);

public sealed record ReportCardSummary(
    int StudentId,
    string StudentNumber,
    string StudentName,
    int ExaminationId,
    string ExaminationName,
    string AcademicYearName,
    string TermName,
    decimal TotalMarks,
    decimal FullMarks,
    decimal OverallPercentage,
    decimal Gpa,
    bool IsPassing,
    IReadOnlyList<ReportCardSubject> Subjects);

public sealed record RevisionHistoryItem(decimal PreviousMarks, decimal NewMarks, string Reason, string RevisedByName, DateTimeOffset RevisedAtUtc);

public sealed record PublishedMarkForRevision(
    int MarkId,
    int StudentId,
    string StudentNumber,
    string StudentName,
    string ClassName,
    string SectionName,
    string SubjectName,
    decimal MarksObtained,
    decimal FullMarks,
    IReadOnlyList<RevisionHistoryItem> History);
