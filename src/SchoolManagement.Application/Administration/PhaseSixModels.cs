namespace SchoolManagement.Application.Administration;

public sealed record PhaseSixFile(byte[] Content, string FileName, string ContentType);

public sealed record ImportRowError(int Row, string Message);

public sealed record StudentImportResult(int ImportedCount, IReadOnlyList<ImportRowError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

public sealed record AnnouncementInput(string Title, string Content, string Audience);

public sealed record AnnouncementSummary(
    int Id,
    string Title,
    string Content,
    string Audience,
    string PublishedByName,
    DateTimeOffset PublishedAtUtc);

public sealed record MessagingTeacherOption(string UserId, string Name, string Subject, string ClassName, string SectionName);

public sealed record MessagingStudentOption(int StudentId, string StudentName, IReadOnlyList<MessagingTeacherOption> Teachers);

public sealed record ConversationSummary(
    int Id,
    int StudentId,
    string StudentName,
    string OtherParticipantName,
    DateTimeOffset LastMessageAtUtc,
    int UnreadMessageCount);

public sealed record MessageAttachmentSummary(int Id, string FileName, string ContentType, long SizeBytes);

public sealed record MessageSummary(
    int Id,
    string SenderUserId,
    string SenderName,
    string Content,
    DateTimeOffset SentAtUtc,
    IReadOnlyList<MessageAttachmentSummary> Attachments);

public sealed record MessageAttachmentUpload(string FileName, string ContentType, ReadOnlyMemory<byte> Content);

public sealed record AuditLogSummary(
    long Id,
    string ActorName,
    string Action,
    string EntityType,
    string EntityId,
    string Details,
    DateTimeOffset OccurredAtUtc);

public sealed record BackgroundJobSummary(
    long Id,
    string JobType,
    string Status,
    string Details,
    int AttemptCount,
    DateTimeOffset RecordedAtUtc);
