using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Administration;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services;

public sealed partial class PhaseSixService
{
    private static readonly string[] StaffRoles =
    [
        SchoolRoles.SuperAdmin, SchoolRoles.AdmissionOfficer, SchoolRoles.Teacher,
        SchoolRoles.Accountant, SchoolRoles.Principal
    ];

    public async Task<IReadOnlyList<AnnouncementSummary>> GetAnnouncementsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(actorUserId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        var isParent = await userManager.IsInRoleAsync(user, SchoolRoles.ParentStudent);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var announcements = await db.Announcements.AsNoTracking()
            .Where(item => item.PublishedAtUtc <= DateTimeOffset.UtcNow
                && (item.Audience == "Everyone" || (isParent ? item.Audience == "Parents" : item.Audience == "Staff")))
            .OrderByDescending(item => item.PublishedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var userIds = announcements.Select(item => item.PublishedByUserId).Distinct().ToArray();
        var profiles = await db.UserProfiles.AsNoTracking().Where(item => userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.FullName, cancellationToken);
        var names = await db.Users.AsNoTracking().Where(item => userIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.UserName ?? item.Email ?? "School staff", cancellationToken);
        return announcements.Select(item => new AnnouncementSummary(
            item.Id,
            item.Title,
            item.Content,
            item.Audience,
            profiles.GetValueOrDefault(item.PublishedByUserId, names.GetValueOrDefault(item.PublishedByUserId, "School staff")),
            item.PublishedAtUtc)).ToArray();
    }

    public async Task CreateAnnouncementAsync(
        string actorUserId,
        AnnouncementInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin, SchoolRoles.Principal);
        var title = input.Title.Trim();
        var content = input.Content.Trim();
        var audience = input.Audience.Trim();
        if (title.Length is 0 or > 160) throw new ArgumentException("Enter an announcement title no longer than 160 characters.");
        if (content.Length is 0 or > 8000) throw new ArgumentException("Enter announcement text no longer than 8,000 characters.");
        if (audience is not ("Everyone" or "Parents" or "Staff")) throw new ArgumentException("Choose Everyone, Parents, or Staff.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var announcement = new Announcement
        {
            Title = title,
            Content = content,
            Audience = audience,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedByUserId = actorUserId
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(cancellationToken);
        await AddAuditAsync(db, actorUserId, "AnnouncementPublished", "Announcement", announcement.Id.ToString(), $"Published to {audience}.");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MessagingStudentOption>> GetParentMessagingOptionsAsync(
        string parentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(parentUserId, cancellationToken, SchoolRoles.ParentStudent);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var children = await db.Students.AsNoTracking()
            .Where(student => db.StudentGuardians.Any(link => link.StudentId == student.Id
                && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == parentUserId)))
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.AcademicYear)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.Section)!.ThenInclude(section => section!.Class)
            .OrderBy(student => student.FullName)
            .ToListAsync(cancellationToken);
        if (children.Count == 0) return [];

        var sectionIds = children.SelectMany(student => student.Enrollments).Where(item => item.Status == Core.Enums.EnrollmentStatus.Active).Select(item => item.SectionId).Distinct().ToArray();
        var yearIds = children.SelectMany(student => student.Enrollments).Where(item => item.Status == Core.Enums.EnrollmentStatus.Active).Select(item => item.AcademicYearId).Distinct().ToArray();
        var assignments = await db.TeacherAssignments.AsNoTracking()
            .Where(item => sectionIds.Contains(item.SectionId) && yearIds.Contains(item.AcademicYearId))
            .Include(item => item.Section)!.ThenInclude(section => section!.Class)
            .Include(item => item.Subject)
            .ToListAsync(cancellationToken);
        var teacherIds = assignments.Select(item => item.TeacherUserId).Distinct().ToArray();
        var profiles = await db.UserProfiles.AsNoTracking().Where(item => teacherIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.FullName, cancellationToken);
        var users = await db.Users.AsNoTracking().Where(item => teacherIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.UserName ?? item.Email ?? "Teacher", cancellationToken);

        return children.Select(student =>
        {
            var activeEnrollments = student.Enrollments.Where(item => item.Status == Core.Enums.EnrollmentStatus.Active).ToArray();
            var teachers = assignments.Where(assignment => activeEnrollments.Any(enrollment =>
                    enrollment.SectionId == assignment.SectionId && enrollment.AcademicYearId == assignment.AcademicYearId))
                .DistinctBy(assignment => assignment.TeacherUserId)
                .Select(assignment => new MessagingTeacherOption(
                    assignment.TeacherUserId,
                    profiles.GetValueOrDefault(assignment.TeacherUserId, users.GetValueOrDefault(assignment.TeacherUserId, "Teacher")),
                    assignment.Subject?.Name ?? "",
                    assignment.Section?.Class?.Name ?? "",
                    assignment.Section?.Name ?? ""))
                .OrderBy(item => item.Name).ToArray();
            return new MessagingStudentOption(student.Id, student.FullName, teachers);
        }).ToArray();
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(actorUserId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
        var isParent = await userManager.IsInRoleAsync(user, SchoolRoles.ParentStudent);
        var isTeacher = await userManager.IsInRoleAsync(user, SchoolRoles.Teacher);
        if (!isParent && !isTeacher) throw new UnauthorizedAccessException("Only parents and teachers can access conversations.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Conversations.AsNoTracking().AsQueryable();
        query = isParent
            ? query.Where(item => item.ParentUserId == actorUserId)
            : query.Where(item => item.TeacherUserId == actorUserId);
        var conversations = await query.Include(item => item.Student)
            .OrderByDescending(item => item.LastMessageAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var otherIds = conversations.Select(item => isParent ? item.TeacherUserId : item.ParentUserId).Distinct().ToArray();
        var profiles = await db.UserProfiles.AsNoTracking().Where(item => otherIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.FullName, cancellationToken);
        var users = await db.Users.AsNoTracking().Where(item => otherIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.UserName ?? item.Email ?? "School account", cancellationToken);
        return conversations.Select(item =>
        {
            var otherId = isParent ? item.TeacherUserId : item.ParentUserId;
            return new ConversationSummary(
                item.Id,
                item.StudentId,
                item.Student?.FullName ?? "Student",
                profiles.GetValueOrDefault(otherId, users.GetValueOrDefault(otherId, "School account")),
                item.LastMessageAtUtc,
                0);
        }).ToArray();
    }

    public async Task<IReadOnlyList<MessageSummary>> GetMessagesAsync(
        string actorUserId,
        int conversationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == conversationId && (item.ParentUserId == actorUserId || item.TeacherUserId == actorUserId), cancellationToken);
        if (conversation is null) throw new UnauthorizedAccessException("This conversation is unavailable for your account.");

        var messages = await db.Messages.AsNoTracking()
            .Where(item => item.ConversationId == conversationId)
            .Include(item => item.Attachments)
            .OrderBy(item => item.SentAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);
        var senderIds = messages.Select(item => item.SenderUserId).Distinct().ToArray();
        var profiles = await db.UserProfiles.AsNoTracking().Where(item => senderIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.FullName, cancellationToken);
        var users = await db.Users.AsNoTracking().Where(item => senderIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.UserName ?? item.Email ?? "School account", cancellationToken);
        return messages.Select(item => new MessageSummary(
            item.Id,
            item.SenderUserId,
            profiles.GetValueOrDefault(item.SenderUserId, users.GetValueOrDefault(item.SenderUserId, "School account")),
            item.Content,
            item.SentAtUtc,
            item.Attachments.OrderBy(attachment => attachment.Id)
                .Select(attachment => new MessageAttachmentSummary(attachment.Id, attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes))
                .ToArray())).ToArray();
    }

    public async Task<int> StartConversationAsync(
        string parentUserId,
        int studentId,
        string teacherUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(parentUserId, cancellationToken, SchoolRoles.ParentStudent);
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var allowed = await db.Enrollments.AsNoTracking().AnyAsync(enrollment =>
            enrollment.StudentId == studentId
            && enrollment.Status == Core.Enums.EnrollmentStatus.Active
            && db.StudentGuardians.Any(link => link.StudentId == studentId
                && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == parentUserId))
            && db.TeacherAssignments.Any(assignment => assignment.SectionId == enrollment.SectionId
                && assignment.AcademicYearId == enrollment.AcademicYearId
                && assignment.TeacherUserId == teacherUserId), cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException("Choose a teacher assigned to one of your linked children.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var conversation = await db.Conversations.SingleOrDefaultAsync(item =>
            item.StudentId == studentId && item.ParentUserId == parentUserId && item.TeacherUserId == teacherUserId, cancellationToken);
        if (conversation is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return conversation.Id;
        }

        conversation = new Conversation
        {
            StudentId = studentId,
            ParentUserId = parentUserId,
            TeacherUserId = teacherUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastMessageAtUtc = DateTimeOffset.UtcNow
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        await AddAuditAsync(db, parentUserId, "ConversationStarted", "Conversation", conversation.Id.ToString(), "Parent opened a teacher conversation.");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return conversation.Id;
    }

    public async Task SendMessageAsync(
        string actorUserId,
        int conversationId,
        string content,
        MessageAttachmentUpload? attachment = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = await db.Conversations.SingleOrDefaultAsync(item =>
            item.Id == conversationId && (item.ParentUserId == actorUserId || item.TeacherUserId == actorUserId), cancellationToken)
            ?? throw new UnauthorizedAccessException("This conversation is unavailable for your account.");
        var cleanedContent = content.Trim();
        if (cleanedContent.Length > 4000) throw new ArgumentException("Messages must be no longer than 4,000 characters.");
        if (cleanedContent.Length == 0 && attachment is null) throw new ArgumentException("Enter a message or attach a document.");

        string? storageKey = null;
        string? extension = null;
        string? contentType = null;
        var safeName = "";
        if (attachment is not null)
        {
            if (attachment.Content.IsEmpty || attachment.Content.Length > MaximumUploadBytes) throw new ArgumentException("Choose a message attachment no larger than 5 MB.");
            safeName = SafeFileName(attachment.FileName);
            extension = Path.GetExtension(safeName).ToLowerInvariant();
            if (extension == ".jpeg") extension = ".jpg";
            var bytes = attachment.Content.Span;
            contentType = extension switch
            {
                ".pdf" when bytes.StartsWith("%PDF-"u8) => "application/pdf",
                ".png" when bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => "image/png",
                ".jpg" when bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff => "image/jpeg",
                _ => null
            };
            if (contentType is null) throw new ArgumentException("Attachments must be valid PDF, PNG, or JPEG files.");
            storageKey = await documentStore.SaveAsync(attachment.Content, extension, cancellationToken);
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var message = new SchoolMessage
            {
                ConversationId = conversationId,
                SenderUserId = actorUserId,
                Content = cleanedContent,
                SentAtUtc = now
            };
            if (storageKey is not null && attachment is not null)
            {
                message.Attachments.Add(new MessageAttachment
                {
                    OriginalFileName = safeName,
                    ContentType = contentType!,
                    StorageKey = storageKey,
                    SizeBytes = attachment.Content.Length,
                    UploadedAtUtc = now
                });
            }
            conversation.LastMessageAtUtc = now;
            db.Messages.Add(message);
            await AddAuditAsync(db, actorUserId, "MessageSent", "Conversation", conversationId.ToString(), storageKey is null ? "Message sent." : "Message sent with a private attachment.");
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (storageKey is not null) await documentStore.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<PrivateDocumentDownload?> GetMessageAttachmentAsync(
        string actorUserId,
        int attachmentId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await db.MessageAttachments.AsNoTracking()
            .Include(item => item.Message)!.ThenInclude(message => message!.Conversation)
            .SingleOrDefaultAsync(item => item.Id == attachmentId, cancellationToken);
        if (attachment?.Message?.Conversation is not { } conversation
            || (conversation.ParentUserId != actorUserId && conversation.TeacherUserId != actorUserId))
        {
            throw new UnauthorizedAccessException("This private attachment is unavailable for your account.");
        }

        var stream = await documentStore.OpenReadAsync(attachment.StorageKey, cancellationToken);
        await AddAuditAsync(db, actorUserId, "MessageAttachmentDownload", "MessageAttachment", attachmentId.ToString(), "Downloaded an authorized private attachment.");
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
        return new PrivateDocumentDownload(stream, attachment.OriginalFileName, attachment.ContentType);
    }
}
