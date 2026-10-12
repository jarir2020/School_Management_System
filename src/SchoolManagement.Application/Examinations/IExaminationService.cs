namespace SchoolManagement.Application.Examinations;

public interface IExaminationService
{
    Task<ExaminationSetupOptions> GetSetupOptionsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task CreateTermAsync(string actorUserId, TermInput input, CancellationToken cancellationToken = default);
    Task CreateExaminationAsync(string actorUserId, ExaminationInput input, CancellationToken cancellationToken = default);
    Task AddExamSubjectAsync(string actorUserId, ExamSubjectInput input, CancellationToken cancellationToken = default);
    Task CreateGradeBandAsync(string actorUserId, GradeBandInput input, CancellationToken cancellationToken = default);
    Task OpenExaminationAsync(string actorUserId, int examinationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExamWorkItem>> GetTeacherWorkAsync(string teacherUserId, CancellationToken cancellationToken = default);
    Task<ExamMarkRegister> GetMarkRegisterAsync(string teacherUserId, int examSubjectId, CancellationToken cancellationToken = default);
    Task SaveMarksAsync(string teacherUserId, int examSubjectId, IReadOnlyList<MarkInput> entries, CancellationToken cancellationToken = default);
    Task SubmitSubjectMarksAsync(string teacherUserId, int examSubjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExamReviewSummary>> GetReviewQueueAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task ReviewExaminationAsync(string reviewerUserId, int examinationId, bool approve, string? remarks, CancellationToken cancellationToken = default);
    Task PublishExaminationAsync(string publisherUserId, int examinationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportCardSummary>> GetMyReportCardsAsync(string parentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublishedMarkForRevision>> GetPublishedMarksForRevisionAsync(string actorUserId, int examinationId, CancellationToken cancellationToken = default);
    Task RevisePublishedMarkAsync(string actorUserId, int markId, decimal newMarks, string reason, CancellationToken cancellationToken = default);
}
