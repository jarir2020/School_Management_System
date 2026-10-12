using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Examinations;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Services;

/// <summary>Owns exam setup, teacher marks, review, publication, and authorized result revisions.</summary>
public sealed class ExaminationService(
    IApplicationDbContextFactory contextFactory,
    UserManager<ApplicationUser> userManager) : IExaminationService
{
    private static readonly string[] ResultManagers = [SchoolRoles.Principal, SchoolRoles.SuperAdmin];

    public async Task<ExaminationSetupOptions> GetSetupOptionsAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var years = await db.AcademicYears.AsNoTracking()
            .OrderByDescending(year => year.StartDate)
            .Select(year => new ExamYearOption(year.Id, year.Name, year.StartDate, year.EndDate))
            .ToListAsync(cancellationToken);
        var terms = await db.Terms.AsNoTracking()
            .Include(term => term.AcademicYear)
            .OrderByDescending(term => term.StartDate)
            .Select(term => new TermSummary(
                term.Id,
                term.AcademicYearId,
                term.AcademicYear!.Name,
                term.Name,
                term.StartDate,
                term.EndDate))
            .ToListAsync(cancellationToken);
        var sections = await db.Sections.AsNoTracking()
            .Include(section => section.Class)
            .OrderBy(section => section.Class!.GradeLevel)
            .ThenBy(section => section.Name)
            .Select(section => new ExamSectionOption(section.Id, section.Class!.Name, section.Name))
            .ToListAsync(cancellationToken);
        var subjects = await db.Subjects.AsNoTracking()
            .OrderBy(subject => subject.Name)
            .Select(subject => new ExamSubjectOption(subject.Id, subject.Name, subject.Code, subject.FullMarks))
            .ToListAsync(cancellationToken);
        var examinations = await MapExaminationsAsync(db, db.Examinations.AsNoTracking(), cancellationToken);
        var bands = await GetGradeBandsAsync(db, null, cancellationToken);
        return new ExaminationSetupOptions(years, terms, sections, subjects, examinations, bands);
    }

    public async Task CreateTermAsync(
        string actorUserId,
        TermInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        var name = RequiredText(input.Name, "Term name", 60);
        if (input.EndDate < input.StartDate)
        {
            throw new ArgumentException("Term end date must be on or after its start date.");
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var year = await db.AcademicYears.SingleOrDefaultAsync(item => item.Id == input.AcademicYearId, cancellationToken)
            ?? throw new KeyNotFoundException("The selected academic year does not exist.");
        if (input.StartDate < year.StartDate || input.EndDate > year.EndDate)
        {
            throw new ArgumentException("Term dates must fall within the selected academic year.");
        }

        if (await db.Terms.AnyAsync(term => term.AcademicYearId == input.AcademicYearId && term.Name == name, cancellationToken))
        {
            throw new InvalidOperationException("A term with this name already exists in the academic year.");
        }

        db.Terms.Add(new Term
        {
            AcademicYearId = input.AcademicYearId,
            Name = name,
            StartDate = input.StartDate,
            EndDate = input.EndDate
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateExaminationAsync(
        string actorUserId,
        ExaminationInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        var name = RequiredText(input.Name, "Examination name", 120);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Terms.AnyAsync(term => term.Id == input.TermId && term.AcademicYearId == input.AcademicYearId, cancellationToken))
        {
            throw new ArgumentException("Choose a term that belongs to the selected academic year.");
        }

        if (await db.Examinations.AnyAsync(exam => exam.AcademicYearId == input.AcademicYearId
                && exam.TermId == input.TermId && exam.Name == name,
                cancellationToken))
        {
            throw new InvalidOperationException("An examination with this name already exists in that term.");
        }

        db.Examinations.Add(new Examination
        {
            AcademicYearId = input.AcademicYearId,
            TermId = input.TermId,
            Name = name,
            Status = ExaminationStatus.Draft,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddExamSubjectAsync(
        string actorUserId,
        ExamSubjectInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        ValidateMarkLimits(input.FullMarks, input.PassMarks);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exam = await db.Examinations.SingleOrDefaultAsync(item => item.Id == input.ExaminationId, cancellationToken)
            ?? throw new KeyNotFoundException("The examination does not exist.");
        if (exam.Status != ExaminationStatus.Draft)
        {
            throw new InvalidOperationException("Subjects can be configured only while an examination is in Draft.");
        }

        if (!await db.Sections.AnyAsync(section => section.Id == input.SectionId, cancellationToken)
            || !await db.Subjects.AnyAsync(subject => subject.Id == input.SubjectId, cancellationToken))
        {
            throw new KeyNotFoundException("Choose an existing section and subject.");
        }

        var assignedTeacherExists = await db.TeacherAssignments.AnyAsync(assignment =>
            assignment.SectionId == input.SectionId
            && assignment.SubjectId == input.SubjectId
            && assignment.AcademicYearId == exam.AcademicYearId,
            cancellationToken);
        if (!assignedTeacherExists)
        {
            throw new InvalidOperationException("Assign a teacher to this subject, section, and academic year before adding it to an examination.");
        }

        if (await db.ExamSubjects.AnyAsync(item => item.ExaminationId == input.ExaminationId
                && item.SectionId == input.SectionId && item.SubjectId == input.SubjectId,
                cancellationToken))
        {
            throw new InvalidOperationException("This subject is already configured for that section in the examination.");
        }

        db.ExamSubjects.Add(new ExamSubject
        {
            ExaminationId = exam.Id,
            SectionId = input.SectionId,
            SubjectId = input.SubjectId,
            FullMarks = input.FullMarks,
            PassMarks = input.PassMarks,
            Status = ExamSubjectStatus.Configured
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateGradeBandAsync(
        string actorUserId,
        GradeBandInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        if (input.MinimumPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(input.MinimumPercentage), "Grade thresholds must be between 0 and 100 percent.");
        }

        if (input.GradePoint is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(input.GradePoint), "Grade points must be between 0 and 10.");
        }

        var grade = RequiredText(input.LetterGrade, "Letter grade", 8).ToUpperInvariant();
        var remark = OptionalText(input.Remark, 120) ?? "";
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.AcademicYears.AnyAsync(year => year.Id == input.AcademicYearId, cancellationToken))
        {
            throw new KeyNotFoundException("The selected academic year does not exist.");
        }

        if (await db.Examinations.AnyAsync(exam => exam.AcademicYearId == input.AcademicYearId
                && exam.Status != ExaminationStatus.Draft,
                cancellationToken))
        {
            throw new InvalidOperationException("Grade bands are locked after an examination is opened. Configure the complete grading scale before opening exams for this academic year.");
        }

        if (await db.GradeBands.AnyAsync(band => band.AcademicYearId == input.AcademicYearId
                && band.MinimumPercentage == input.MinimumPercentage,
                cancellationToken))
        {
            throw new InvalidOperationException("A grade band already uses that percentage threshold for the academic year.");
        }

        db.GradeBands.Add(new GradeBand
        {
            AcademicYearId = input.AcademicYearId,
            MinimumPercentage = input.MinimumPercentage,
            LetterGrade = grade,
            GradePoint = input.GradePoint,
            IsPassing = input.IsPassing,
            Remark = remark
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task OpenExaminationAsync(
        string actorUserId,
        int examinationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exam = await db.Examinations.Include(item => item.Subjects)
            .SingleOrDefaultAsync(item => item.Id == examinationId, cancellationToken)
            ?? throw new KeyNotFoundException("The examination does not exist.");
        if (exam.Status != ExaminationStatus.Draft)
        {
            throw new InvalidOperationException("Only a Draft examination can be opened for marks entry.");
        }

        if (exam.Subjects.Count == 0)
        {
            throw new InvalidOperationException("Add at least one section and subject before opening the examination.");
        }

        if (!await db.GradeBands.AnyAsync(band => band.AcademicYearId == exam.AcademicYearId && band.MinimumPercentage == 0, cancellationToken))
        {
            throw new InvalidOperationException("Configure a grade band starting at 0 percent before opening this examination.");
        }

        exam.Status = ExaminationStatus.InProgress;
        foreach (var subject in exam.Subjects) subject.Status = ExamSubjectStatus.InProgress;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamWorkItem>> GetTeacherWorkAsync(
        string teacherUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = WorkItemQuery(db).Where(item => item.Examination!.Status == ExaminationStatus.InProgress
            && item.Status == ExamSubjectStatus.InProgress
            && db.TeacherAssignments.Any(assignment => assignment.TeacherUserId == teacherUserId
                && assignment.SectionId == item.SectionId
                && assignment.SubjectId == item.SubjectId
                && assignment.AcademicYearId == item.Examination.AcademicYearId));
        return await MapWorkItemsAsync(query, cancellationToken);
    }

    public async Task<ExamMarkRegister> GetMarkRegisterAsync(
        string teacherUserId,
        int examSubjectId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var work = await GetTeacherWorkItemAsync(db, teacherUserId, examSubjectId, cancellationToken);
        var roster = await GetActiveRosterAsync(db, work.SectionId, work.AcademicYearId, cancellationToken);
        var studentIds = roster.Select(student => student.StudentId).ToArray();
        var marks = await db.StudentMarks.AsNoTracking()
            .Where(mark => mark.ExamSubjectId == examSubjectId && studentIds.Contains(mark.StudentId))
            .ToDictionaryAsync(mark => mark.StudentId, mark => mark.MarksObtained, cancellationToken);
        var students = roster.Select(student => new StudentMarkEntry(
                student.StudentId,
                student.StudentNumber,
                student.StudentName,
                marks.GetValueOrDefault(student.StudentId)))
            .ToArray();
        return new ExamMarkRegister(work, students);
    }

    public async Task SaveMarksAsync(
        string teacherUserId,
        int examSubjectId,
        IReadOnlyList<MarkInput> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Select(entry => entry.StudentId).Distinct().Count() != entries.Count)
        {
            throw new ArgumentException("A student appears more than once in this marks submission.");
        }

        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var work = await GetTeacherWorkItemAsync(db, teacherUserId, examSubjectId, cancellationToken);
        var roster = await GetActiveRosterAsync(db, work.SectionId, work.AcademicYearId, cancellationToken);
        var expectedIds = roster.Select(student => student.StudentId).ToHashSet();
        if (entries.Count != expectedIds.Count || !expectedIds.SetEquals(entries.Select(entry => entry.StudentId)))
        {
            throw new InvalidOperationException("The active roster changed. Reload the register before saving marks.");
        }

        if (entries.Any(entry => entry.MarksObtained < 0 || entry.MarksObtained > work.FullMarks))
        {
            throw new ArgumentOutOfRangeException(nameof(entries), $"Each mark must be from 0 to {work.FullMarks:0.##}.");
        }

        var existing = await db.StudentMarks
            .Where(mark => mark.ExamSubjectId == examSubjectId && expectedIds.Contains(mark.StudentId))
            .ToDictionaryAsync(mark => mark.StudentId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.StudentId, out var mark))
            {
                mark.MarksObtained = entry.MarksObtained;
                mark.UpdatedByUserId = teacherUserId;
                mark.UpdatedAtUtc = now;
            }
            else
            {
                db.StudentMarks.Add(new StudentMark
                {
                    ExamSubjectId = examSubjectId,
                    StudentId = entry.StudentId,
                    MarksObtained = entry.MarksObtained,
                    EnteredByUserId = teacherUserId,
                    UpdatedByUserId = teacherUserId,
                    EnteredAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = teacherUserId,
            Action = "MarksSaved",
            EntityType = "ExamSubject",
            EntityId = examSubjectId.ToString(),
            Details = $"Saved marks for {entries.Count} roster records.",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SubmitSubjectMarksAsync(
        string teacherUserId,
        int examSubjectId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(teacherUserId, cancellationToken, SchoolRoles.Teacher);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var work = await GetTeacherWorkItemAsync(db, teacherUserId, examSubjectId, cancellationToken);
        var activeStudentIds = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.SectionId == work.SectionId
                && enrollment.AcademicYearId == work.AcademicYearId
                && enrollment.Status == EnrollmentStatus.Active
                && enrollment.Student!.Status == StudentStatus.Active)
            .Select(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);
        var rosterCount = activeStudentIds.Count;
        var marks = await db.StudentMarks.Where(mark => mark.ExamSubjectId == examSubjectId).ToListAsync(cancellationToken);
        if (rosterCount == 0 || marks.Count != rosterCount
            || !activeStudentIds.ToHashSet().SetEquals(marks.Select(mark => mark.StudentId))
            || marks.Any(mark => !mark.MarksObtained.HasValue))
        {
            throw new InvalidOperationException("Save marks for every active student before submitting this subject.");
        }

        var item = await db.ExamSubjects.SingleAsync(subject => subject.Id == examSubjectId, cancellationToken);
        item.Status = ExamSubjectStatus.Submitted;
        item.SubmittedAtUtc = DateTimeOffset.UtcNow;
        var exam = await db.Examinations.Include(candidate => candidate.Subjects)
            .SingleAsync(candidate => candidate.Id == work.ExaminationId, cancellationToken);
        if (exam.Subjects.All(subject => subject.Status == ExamSubjectStatus.Submitted))
        {
            exam.Status = ExaminationStatus.Submitted;
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = teacherUserId,
            Action = "SubjectMarksSubmitted",
            EntityType = "ExamSubject",
            EntityId = examSubjectId.ToString(),
            Details = "Submitted a complete active class roster for review.",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExamReviewSummary>> GetReviewQueueAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, ResultManagers);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exams = await db.Examinations.AsNoTracking()
            .Where(exam => exam.Status == ExaminationStatus.Submitted || exam.Status == ExaminationStatus.Approved)
            .Include(exam => exam.AcademicYear)
            .Include(exam => exam.Term)
            .Include(exam => exam.Subjects)
            .Include(exam => exam.Subjects).ThenInclude(subject => subject.Marks).ThenInclude(mark => mark.Student)
            .Include(exam => exam.Subjects).ThenInclude(subject => subject.Section)!.ThenInclude(section => section!.Class)
            .Include(exam => exam.Subjects).ThenInclude(subject => subject.Subject)
            .Include(exam => exam.Approvals)
            .OrderByDescending(exam => exam.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var reviewerIds = exams.SelectMany(exam => exam.Approvals.Select(approval => approval.ReviewerUserId)).Distinct().ToArray();
        var names = await GetUserNamesAsync(db, reviewerIds, cancellationToken);
        return exams.Select(exam => new ExamReviewSummary(
                exam.Id,
                exam.Name,
                exam.AcademicYear?.Name ?? "",
                exam.Term?.Name ?? "",
                exam.Status,
                exam.Subjects.Count,
                exam.Subjects.Sum(subject => subject.Marks.Count),
                exam.Subjects.Max(subject => subject.SubmittedAtUtc)
                    ?? exam.CreatedAtUtc,
                exam.Subjects.OrderBy(subject => subject.Section!.Class!.GradeLevel)
                    .ThenBy(subject => subject.Section!.Name)
                    .ThenBy(subject => subject.Subject!.Name)
                    .Select(subject => new ExamReviewSubjectSummary(
                        subject.Section?.Class?.Name ?? "",
                        subject.Section?.Name ?? "",
                        subject.Subject?.Name ?? "",
                        subject.FullMarks,
                        subject.PassMarks,
                        subject.Marks.OrderBy(mark => mark.Student!.FullName)
                            .Select(mark => new ExamReviewMark(
                                mark.Student?.StudentNumber ?? "",
                                mark.Student?.FullName ?? "Student",
                                mark.MarksObtained ?? throw new InvalidOperationException("A submitted examination contains a blank mark.")))
                            .ToArray()))
                    .ToArray(),
                exam.Approvals.OrderBy(approval => approval.ReviewedAtUtc)
                    .Select(approval => new ResultApprovalSummary(
                        approval.Decision,
                        names.GetValueOrDefault(approval.ReviewerUserId, approval.ReviewerUserId),
                        approval.Remarks,
                        approval.ReviewedAtUtc))
                    .ToArray()))
            .ToArray();
    }

    public async Task ReviewExaminationAsync(
        string reviewerUserId,
        int examinationId,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(reviewerUserId, cancellationToken, ResultManagers);
        var cleanRemarks = OptionalText(remarks, 1000) ?? "";
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var exam = await db.Examinations.Include(item => item.Subjects)
            .SingleOrDefaultAsync(item => item.Id == examinationId, cancellationToken)
            ?? throw new KeyNotFoundException("The examination does not exist.");
        if (exam.Status != ExaminationStatus.Submitted)
        {
            throw new InvalidOperationException("Only a fully submitted examination can be reviewed.");
        }

        if (await db.StudentMarks.AnyAsync(mark => mark.ExamSubject!.ExaminationId == examinationId
                && (mark.EnteredByUserId == reviewerUserId || mark.UpdatedByUserId == reviewerUserId),
                cancellationToken))
        {
            throw new InvalidOperationException("A user who entered marks in this examination cannot review its results.");
        }

        if (!approve && string.IsNullOrWhiteSpace(cleanRemarks))
        {
            throw new ArgumentException("Add a reason when rejecting examination results.", nameof(remarks));
        }

        db.ResultApprovals.Add(new ResultApproval
        {
            ExaminationId = examinationId,
            Decision = approve ? ResultApprovalDecision.Approved : ResultApprovalDecision.Rejected,
            ReviewerUserId = reviewerUserId,
            Remarks = cleanRemarks,
            ReviewedAtUtc = DateTimeOffset.UtcNow
        });
        exam.Status = approve ? ExaminationStatus.Approved : ExaminationStatus.InProgress;
        if (!approve)
        {
            foreach (var subject in exam.Subjects)
            {
                subject.Status = ExamSubjectStatus.InProgress;
                subject.SubmittedAtUtc = null;
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = reviewerUserId,
            Action = approve ? "ExaminationApproved" : "ExaminationReturned",
            EntityType = "Examination",
            EntityId = examinationId.ToString(),
            Details = approve ? "Results approved for publication." : "Results returned for correction.",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task PublishExaminationAsync(
        string publisherUserId,
        int examinationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(publisherUserId, cancellationToken, ResultManagers);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exam = await db.Examinations.SingleOrDefaultAsync(item => item.Id == examinationId, cancellationToken)
            ?? throw new KeyNotFoundException("The examination does not exist.");
        if (exam.Status != ExaminationStatus.Approved)
        {
            throw new InvalidOperationException("Only approved results can be published.");
        }

        if (!await db.GradeBands.AnyAsync(band => band.AcademicYearId == exam.AcademicYearId && band.MinimumPercentage == 0, cancellationToken))
        {
            throw new InvalidOperationException("A grade band starting at 0 percent is required before publication.");
        }

        exam.Status = ExaminationStatus.Published;
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = publisherUserId,
            Action = "ExaminationPublished",
            EntityType = "Examination",
            EntityId = examinationId.ToString(),
            Details = "Published approved examination results.",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReportCardSummary>> GetMyReportCardsAsync(
        string parentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(parentUserId, cancellationToken, SchoolRoles.ParentStudent);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var marks = await db.StudentMarks.AsNoTracking()
            .Where(mark => mark.MarksObtained.HasValue
                && mark.ExamSubject!.Examination!.Status == ExaminationStatus.Published
                && db.StudentGuardians.Any(link => link.StudentId == mark.StudentId
                    && db.GuardianAccountLinks.Any(account => account.GuardianId == link.GuardianId && account.UserId == parentUserId)))
            .Include(mark => mark.Student)
            .Include(mark => mark.ExamSubject)!.ThenInclude(item => item!.Subject)
            .Include(mark => mark.ExamSubject)!.ThenInclude(item => item!.Section)!.ThenInclude(section => section!.Class)
            .Include(mark => mark.ExamSubject)!.ThenInclude(item => item!.Examination)!.ThenInclude(exam => exam!.AcademicYear)
            .Include(mark => mark.ExamSubject)!.ThenInclude(item => item!.Examination)!.ThenInclude(exam => exam!.Term)
            .OrderBy(mark => mark.ExamSubject!.Examination!.CreatedAtUtc)
            .ThenBy(mark => mark.Student!.FullName)
            .ToListAsync(cancellationToken);
        var yearIds = marks.Select(mark => mark.ExamSubject!.Examination!.AcademicYearId).Distinct().ToArray();
        var allBands = await GetGradeBandsAsync(db, yearIds, cancellationToken);
        var bandsByYear = allBands.GroupBy(band => band.AcademicYearId).ToDictionary(group => group.Key, group => (IReadOnlyList<GradeBandSummary>)group.ToArray());

        return marks.GroupBy(mark => new { mark.StudentId, mark.ExamSubject!.ExaminationId })
            .Select(group =>
            {
                var first = group.First();
                var exam = first.ExamSubject!.Examination!;
                return BuildReportCard(first.Student!, exam, group.ToArray(), bandsByYear.GetValueOrDefault(exam.AcademicYearId, []));
            })
            .OrderByDescending(card => card.ExaminationId)
            .ThenBy(card => card.StudentName)
            .ToArray();
    }

    public async Task<IReadOnlyList<PublishedMarkForRevision>> GetPublishedMarksForRevisionAsync(
        string actorUserId,
        int examinationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exam = await db.Examinations.SingleOrDefaultAsync(item => item.Id == examinationId, cancellationToken)
            ?? throw new KeyNotFoundException("The examination does not exist.");
        if (exam.Status != ExaminationStatus.Published)
        {
            throw new InvalidOperationException("Only a published examination can be revised.");
        }

        var marks = await db.StudentMarks.AsNoTracking()
            .Where(mark => mark.ExamSubject!.ExaminationId == examinationId && mark.MarksObtained.HasValue)
            .Include(mark => mark.Student)
            .Include(mark => mark.ExamSubject)!.ThenInclude(item => item!.Section)!.ThenInclude(section => section!.Class)
            .Include(mark => mark.ExamSubject)!.ThenInclude(item => item!.Subject)
            .Include(mark => mark.Revisions)
            .OrderBy(mark => mark.ExamSubject!.Section!.Class!.GradeLevel)
            .ThenBy(mark => mark.ExamSubject!.Section!.Name)
            .ThenBy(mark => mark.Student!.FullName)
            .ThenBy(mark => mark.ExamSubject!.Subject!.Name)
            .ToListAsync(cancellationToken);
        var editorIds = marks.SelectMany(mark => mark.Revisions.Select(revision => revision.RevisedByUserId)).Distinct().ToArray();
        var names = await GetUserNamesAsync(db, editorIds, cancellationToken);
        return marks.Select(mark => new PublishedMarkForRevision(
                mark.Id,
                mark.StudentId,
                mark.Student?.StudentNumber ?? "",
                mark.Student?.FullName ?? "Student",
                mark.ExamSubject?.Section?.Class?.Name ?? "",
                mark.ExamSubject?.Section?.Name ?? "",
                mark.ExamSubject?.Subject?.Name ?? "",
                mark.MarksObtained!.Value,
                mark.ExamSubject!.FullMarks,
                mark.Revisions.OrderByDescending(revision => revision.RevisedAtUtc)
                    .Select(revision => new RevisionHistoryItem(
                        revision.PreviousMarks,
                        revision.NewMarks,
                        revision.Reason,
                        names.GetValueOrDefault(revision.RevisedByUserId, revision.RevisedByUserId),
                        revision.RevisedAtUtc))
                    .ToArray()))
            .ToArray();
    }

    public async Task RevisePublishedMarkAsync(
        string actorUserId,
        int markId,
        decimal newMarks,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin);
        var cleanReason = RequiredText(reason, "Revision reason", 500);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mark = await db.StudentMarks.Include(item => item.ExamSubject)!.ThenInclude(subject => subject!.Examination)
            .SingleOrDefaultAsync(item => item.Id == markId, cancellationToken)
            ?? throw new KeyNotFoundException("The student mark does not exist.");
        if (mark.ExamSubject?.Examination?.Status != ExaminationStatus.Published)
        {
            throw new InvalidOperationException("Only marks in a published examination can be revised.");
        }

        if (newMarks < 0 || newMarks > mark.ExamSubject.FullMarks)
        {
            throw new ArgumentOutOfRangeException(nameof(newMarks), $"Marks must be from 0 to {mark.ExamSubject.FullMarks:0.##}.");
        }

        if (mark.MarksObtained == newMarks)
        {
            throw new InvalidOperationException("The new mark is the same as the published mark.");
        }

        db.ResultRevisions.Add(new ResultRevision
        {
            StudentMarkId = mark.Id,
            PreviousMarks = mark.MarksObtained ?? throw new InvalidOperationException("A published mark cannot be blank."),
            NewMarks = newMarks,
            Reason = cleanReason,
            RevisedByUserId = actorUserId,
            RevisedAtUtc = DateTimeOffset.UtcNow
        });
        mark.MarksObtained = newMarks;
        mark.UpdatedByUserId = actorUserId;
        mark.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = "PublishedMarkRevised",
            EntityType = "StudentMark",
            EntityId = mark.Id.ToString(),
            Details = "A reasoned revision was added to a published mark.",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static IQueryable<ExamSubject> WorkItemQuery(IApplicationDbContext db) => db.ExamSubjects.AsNoTracking()
        .Include(item => item.Examination)!.ThenInclude(exam => exam!.AcademicYear)
        .Include(item => item.Examination)!.ThenInclude(exam => exam!.Term)
        .Include(item => item.Section)!.ThenInclude(section => section!.Class)
        .Include(item => item.Subject);

    private async Task<ExamWorkItem> GetTeacherWorkItemAsync(
        IApplicationDbContext db,
        string teacherUserId,
        int examSubjectId,
        CancellationToken cancellationToken)
    {
        var query = WorkItemQuery(db).Where(item => item.Id == examSubjectId
            && item.Status == ExamSubjectStatus.InProgress
            && item.Examination!.Status == ExaminationStatus.InProgress
            && db.TeacherAssignments.Any(assignment => assignment.TeacherUserId == teacherUserId
                && assignment.SectionId == item.SectionId
                && assignment.SubjectId == item.SubjectId
                && assignment.AcademicYearId == item.Examination.AcademicYearId));
        var work = (await MapWorkItemsAsync(query, cancellationToken)).SingleOrDefault();
        return work ?? throw new UnauthorizedAccessException("This examination subject is not open or is not assigned to your account.");
    }

    private static async Task<List<StudentMarkEntry>> GetActiveRosterAsync(
        IApplicationDbContext db,
        int sectionId,
        int academicYearId,
        CancellationToken cancellationToken) => await db.Enrollments.AsNoTracking()
        .Where(enrollment => enrollment.SectionId == sectionId
            && enrollment.AcademicYearId == academicYearId
            && enrollment.Status == EnrollmentStatus.Active
            && enrollment.Student!.Status == StudentStatus.Active)
        .OrderBy(enrollment => enrollment.Student!.FullName)
        .Select(enrollment => new StudentMarkEntry(
            enrollment.StudentId,
            enrollment.Student!.StudentNumber,
            enrollment.Student.FullName,
            null))
        .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<ExamWorkItem>> MapWorkItemsAsync(
        IQueryable<ExamSubject> query,
        CancellationToken cancellationToken)
    {
        var items = await query
            .OrderByDescending(item => item.Examination!.CreatedAtUtc)
            .ThenBy(item => item.Section!.Class!.GradeLevel)
            .ThenBy(item => item.Section!.Name)
            .ThenBy(item => item.Subject!.Name)
            .ToListAsync(cancellationToken);
        return items.Select(item => new ExamWorkItem(
                item.Id,
                item.ExaminationId,
                item.Examination?.Name ?? "",
                item.Examination?.Status ?? ExaminationStatus.Draft,
                item.Examination?.AcademicYearId ?? 0,
                item.Examination?.AcademicYear?.Name ?? "",
                item.Examination?.Term?.Name ?? "",
                item.SectionId,
                item.Section?.Class?.Name ?? "",
                item.Section?.Name ?? "",
                item.SubjectId,
                item.Subject?.Name ?? "",
                item.Subject?.Code ?? "",
                item.FullMarks,
                item.PassMarks,
                item.Status))
            .ToArray();
    }

    private async Task<IReadOnlyList<ExaminationSummary>> MapExaminationsAsync(
        IApplicationDbContext db,
        IQueryable<Examination> query,
        CancellationToken cancellationToken)
    {
        var exams = await query
            .Include(exam => exam.AcademicYear)
            .Include(exam => exam.Term)
            .Include(exam => exam.Subjects).ThenInclude(item => item.Section)!.ThenInclude(section => section!.Class)
            .Include(exam => exam.Subjects).ThenInclude(item => item.Subject)
            .OrderByDescending(exam => exam.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return exams.Select(exam => new ExaminationSummary(
                exam.Id,
                exam.AcademicYearId,
                exam.AcademicYear?.Name ?? "",
                exam.TermId,
                exam.Term?.Name ?? "",
                exam.Name,
                exam.Status,
                exam.CreatedAtUtc,
                exam.Subjects.Count,
                exam.Subjects.Count(item => item.Status == ExamSubjectStatus.Submitted),
                exam.Subjects.OrderBy(item => item.Section!.Class!.GradeLevel)
                    .ThenBy(item => item.Section!.Name)
                    .ThenBy(item => item.Subject!.Name)
                    .Select(item => new ExamSubjectSummary(
                        item.Id,
                        item.SectionId,
                        item.Section?.Class?.Name ?? "",
                        item.Section?.Name ?? "",
                        item.SubjectId,
                        item.Subject?.Name ?? "",
                        item.Subject?.Code ?? "",
                        item.FullMarks,
                        item.PassMarks,
                        item.Status))
                    .ToArray()))
            .ToArray();
    }

    private async Task<IReadOnlyList<GradeBandSummary>> GetGradeBandsAsync(
        IApplicationDbContext db,
        int[]? academicYearIds,
        CancellationToken cancellationToken)
    {
        var query = db.GradeBands.AsNoTracking().Include(band => band.AcademicYear).AsQueryable();
        if (academicYearIds is not null)
        {
            query = query.Where(band => academicYearIds.Contains(band.AcademicYearId));
        }

        return await query.OrderBy(band => band.AcademicYearId).ThenByDescending(band => band.MinimumPercentage)
            .Select(band => new GradeBandSummary(
                band.Id,
                band.AcademicYearId,
                band.AcademicYear!.Name,
                band.MinimumPercentage,
                band.LetterGrade,
                band.GradePoint,
                band.IsPassing,
                band.Remark))
            .ToListAsync(cancellationToken);
    }

    private ReportCardSummary BuildReportCard(
        Student student,
        Examination exam,
        StudentMark[] marks,
        IReadOnlyList<GradeBandSummary> gradeBands)
    {
        var subjects = marks.Where(mark => mark.MarksObtained.HasValue)
            .Select(mark =>
            {
                var examSubject = mark.ExamSubject!;
                var grade = GradeCalculator.Calculate(mark.MarksObtained!.Value, examSubject.FullMarks, examSubject.PassMarks, gradeBands);
                return new ReportCardSubject(
                    examSubject.Section?.Class?.Name ?? "",
                    examSubject.Section?.Name ?? "",
                    examSubject.Subject?.Name ?? "",
                    mark.MarksObtained.Value,
                    examSubject.FullMarks,
                    examSubject.PassMarks,
                    grade);
            })
            .OrderBy(item => item.ClassName)
            .ThenBy(item => item.SectionName)
            .ThenBy(item => item.SubjectName)
            .ToArray();
        var total = subjects.Sum(item => item.MarksObtained);
        var full = subjects.Sum(item => item.FullMarks);
        var percentage = full == 0 ? 0 : Math.Round(total * 100m / full, 2);
        var gpa = subjects.Length == 0 ? 0 : Math.Round(subjects.Average(item => item.Grade.GradePoint), 2);
        return new ReportCardSummary(
            student.Id,
            student.StudentNumber,
            student.FullName,
            exam.Id,
            exam.Name,
            exam.AcademicYear?.Name ?? "",
            exam.Term?.Name ?? "",
            total,
            full,
            percentage,
            gpa,
            subjects.Length > 0 && subjects.All(item => item.Grade.IsPassing),
            subjects);
    }

    private async Task<Dictionary<string, string>> GetUserNamesAsync(
        IApplicationDbContext db,
        string[] userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Length == 0) return new Dictionary<string, string>();
        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.FullName, cancellationToken);
        var emails = await userManager.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.Id, cancellationToken);
        return userIds.Distinct().ToDictionary(id => id, id => profiles.GetValueOrDefault(id, emails.GetValueOrDefault(id, id)));
    }

    private async Task<ApplicationUser> RequireUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("A signed-in school account is required.");
        return await userManager.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("The signed-in school account no longer exists.");
    }

    private async Task EnsureRoleAsync(string userId, CancellationToken cancellationToken, params string[] allowedRoles)
    {
        _ = cancellationToken;
        var user = await RequireUserAsync(userId);
        foreach (var role in allowedRoles)
        {
            if (await userManager.IsInRoleAsync(user, role)) return;
        }

        throw new UnauthorizedAccessException("Your school account is not allowed to perform this action.");
    }

    private static void ValidateMarkLimits(decimal fullMarks, decimal passMarks)
    {
        if (fullMarks is <= 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(fullMarks), "Full marks must be greater than zero and no more than 10,000.");
        }

        if (passMarks < 0 || passMarks > fullMarks)
        {
            throw new ArgumentOutOfRangeException(nameof(passMarks), "Pass marks must be between zero and full marks.");
        }
    }

    private static string RequiredText(string? value, string label, int maximumLength)
    {
        var text = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength)
        {
            throw new ArgumentException($"{label} is required and must be {maximumLength} characters or fewer.");
        }

        return text;
    }

    private static string? OptionalText(string? value, int maximumLength)
    {
        var text = value?.Trim();
        if (text?.Length > maximumLength) throw new ArgumentException($"Text must be {maximumLength} characters or fewer.");
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
