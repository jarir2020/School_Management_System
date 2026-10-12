using SchoolManagement.Application.Admissions;

namespace SchoolManagement.Application.Administration;

public interface IPhaseSixService
{
    Task<PhaseSixFile> CreateStudentImportTemplateAsync(CancellationToken cancellationToken = default);
    Task<PhaseSixFile> CreateMarksImportTemplateAsync(string teacherUserId, int examSubjectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportRowError>> ImportMarksAsync(string teacherUserId, int examSubjectId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<PhaseSixFile> ExportStudentsExcelAsync(StudentSearchFilter filter, string actorUserId, CancellationToken cancellationToken = default);
    Task<StudentImportResult> ImportStudentsAsync(string actorUserId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task<PhaseSixFile> ExportFinanceReportExcelAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task<PhaseSixFile> RenderInvoicePdfAsync(string actorUserId, int invoiceId, CancellationToken cancellationToken = default);
    Task<PhaseSixFile> RenderReceiptPdfAsync(string actorUserId, int paymentId, CancellationToken cancellationToken = default);
    Task<PhaseSixFile> RenderReportCardPdfAsync(string actorUserId, int studentId, int examinationId, CancellationToken cancellationToken = default);
    Task<PhaseSixFile> ExportStudentDocumentsZipAsync(string actorUserId, IReadOnlyCollection<int> studentIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AnnouncementSummary>> GetAnnouncementsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task CreateAnnouncementAsync(string actorUserId, AnnouncementInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessagingStudentOption>> GetParentMessagingOptionsAsync(string parentUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageSummary>> GetMessagesAsync(string actorUserId, int conversationId, CancellationToken cancellationToken = default);
    Task<int> StartConversationAsync(string parentUserId, int studentId, string teacherUserId, CancellationToken cancellationToken = default);
    Task SendMessageAsync(string actorUserId, int conversationId, string content, MessageAttachmentUpload? attachment = null, CancellationToken cancellationToken = default);
    Task<PrivateDocumentDownload?> GetMessageAttachmentAsync(string actorUserId, int attachmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundJobSummary>> GetBackgroundJobLogsAsync(string actorUserId, CancellationToken cancellationToken = default);
    Task QueueEmailAsync(string recipient, string subject, string htmlBody, string deduplicationKey, CancellationToken cancellationToken = default);
    Task QueueOverdueReminderEmailsAsync(CancellationToken cancellationToken = default);
    Task<int> DispatchPendingEmailsAsync(CancellationToken cancellationToken = default);
}
