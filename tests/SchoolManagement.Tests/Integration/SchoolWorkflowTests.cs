using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Examinations;
using SchoolManagement.Application.Finance;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Services;
using SchoolManagement.Tests.Support;

namespace SchoolManagement.Tests.Integration;

public sealed class SchoolWorkflowTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdmissionApprovalIsIdempotentAndCannotOverfillASection()
    {
        await using var database = await SqliteSchoolDatabase.CreateAsync(TestToken);
        var reviewer = await database.CreateUserAsync("reviewer@example.test");
        int academicYearId;
        int sectionId;
        await using (var db = await database.CreateContextAsync(TestToken))
        {
            var year = new AcademicYear
            {
                Name = "2026",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                IsActive = true
            };
            var schoolClass = new SchoolClass { Name = "Grade 1", GradeLevel = 1 };
            var section = new Section { Name = "A", Capacity = 1, Class = schoolClass };
            db.AcademicYears.Add(year);
            db.Classes.Add(schoolClass);
            db.Sections.Add(section);
            await db.SaveChangesAsync(TestToken);
            academicYearId = year.Id;
            sectionId = section.Id;
        }

        var service = database.CreateAdmissionService();
        var first = await service.SubmitAsync(NewApplication(academicYearId, sectionId, "Nadia Rahman"), [], TestToken);
        var second = await service.SubmitAsync(NewApplication(academicYearId, sectionId, "Rafi Ahmed"), [], TestToken);
        int firstId;
        int secondId;
        await using (var db = await database.CreateContextAsync(TestToken))
        {
            firstId = (await db.AdmissionApplications.SingleAsync(item => item.ApplicationNumber == first.ApplicationNumber, TestToken)).Id;
            secondId = (await db.AdmissionApplications.SingleAsync(item => item.ApplicationNumber == second.ApplicationNumber, TestToken)).Id;
        }

        var approved = new AdmissionReviewInput(AdmissionDecision.Approved, sectionId, "Documents checked.");
        await service.DecideAsync(firstId, approved, reviewer.Id, TestToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DecideAsync(firstId, approved, reviewer.Id, TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DecideAsync(secondId, approved, reviewer.Id, TestToken));

        await using var verify = await database.CreateContextAsync(TestToken);
        Assert.Equal(1, await verify.Students.CountAsync(TestToken));
        Assert.Equal(1, await verify.Enrollments.CountAsync(item => item.Status == EnrollmentStatus.Active, TestToken));
        Assert.Equal(1, await verify.AdmissionReviews.CountAsync(TestToken));
        Assert.Equal(AdmissionStatus.PendingReview, (await verify.AdmissionApplications.SingleAsync(item => item.Id == secondId, TestToken)).Status);
    }

    [Fact]
    public async Task TeacherCannotEditAnotherTeachersExamAndMarksRespectFullMarks()
    {
        await using var database = await SqliteSchoolDatabase.CreateAsync(TestToken);
        var teacher = await database.CreateUserAsync("teacher@example.test", SchoolRoles.Teacher);
        var otherTeacher = await database.CreateUserAsync("other-teacher@example.test", SchoolRoles.Teacher);
        int examSubjectId;
        int studentId;
        await using (var db = await database.CreateContextAsync(TestToken))
        {
            var year = new AcademicYear { Name = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
            var schoolClass = new SchoolClass { Name = "Grade 5", GradeLevel = 5 };
            var section = new Section { Name = "A", Capacity = 30, Class = schoolClass };
            var subject = new Subject { Name = "Mathematics", Code = "MATH5", FullMarks = 100 };
            var term = new Term { AcademicYear = year, Name = "Term 1", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30) };
            var exam = new Examination { AcademicYear = year, Term = term, Name = "Midyear", CreatedByUserId = teacher.Id, Status = ExaminationStatus.InProgress };
            var examSubject = new ExamSubject { Examination = exam, Section = section, Subject = subject, FullMarks = 100, PassMarks = 40, Status = ExamSubjectStatus.InProgress };
            var application = new AdmissionApplication
            {
                ApplicationNumber = "APP-TEST-STUDENT-1",
                AcademicYear = year,
                RequestedSection = section,
                StudentFullName = "Sami Islam",
                DateOfBirth = new DateOnly(2015, 2, 4),
                GuardianFullName = "Parent One",
                GuardianRelationship = "Parent",
                GuardianPhone = "+8801712345678",
                Status = AdmissionStatus.Approved
            };
            var student = new Student
            {
                AdmissionApplication = application,
                StudentNumber = "STU-2026-0001",
                FullName = "Sami Islam",
                DateOfBirth = application.DateOfBirth,
                Status = StudentStatus.Active
            };

            db.AcademicYears.Add(year);
            db.Classes.Add(schoolClass);
            db.Sections.Add(section);
            db.Subjects.Add(subject);
            db.Terms.Add(term);
            db.Examinations.Add(exam);
            db.ExamSubjects.Add(examSubject);
            db.AdmissionApplications.Add(application);
            db.Students.Add(student);
            db.Enrollments.Add(new Enrollment { Student = student, AcademicYear = year, Section = section, Status = EnrollmentStatus.Active });
            db.TeacherAssignments.Add(new TeacherAssignment { TeacherUserId = teacher.Id, Section = section, Subject = subject, AcademicYear = year });
            await db.SaveChangesAsync(TestToken);
            examSubjectId = examSubject.Id;
            studentId = student.Id;
        }

        using var scope = database.CreateScope();
        var service = new ExaminationService(database.ApplicationDbContextFactory, scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveMarksAsync(otherTeacher.Id, examSubjectId, [new MarkInput(studentId, 50m)], TestToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveMarksAsync(teacher.Id, examSubjectId, [new MarkInput(studentId, 101m)], TestToken));

        await service.SaveMarksAsync(teacher.Id, examSubjectId, [new MarkInput(studentId, 87.5m)], TestToken);
        await using var verify = await database.CreateContextAsync(TestToken);
        var mark = await verify.StudentMarks.SingleAsync(TestToken);
        Assert.Equal(studentId, mark.StudentId);
        Assert.Equal(87.5m, mark.MarksObtained);
    }

    [Fact]
    public async Task UnderpaidGatewayCallbackIsHeldForReviewWithoutCreatingAReceipt()
    {
        await using var database = await SqliteSchoolDatabase.CreateAsync(TestToken);
        var user = await database.CreateUserAsync("payer@example.test");
        var (paymentId, _) = await SeedGatewayPaymentAsync(database, user.Id, TestToken);
        var gateway = new FakeSslCommerzGateway(ValidPayment("txn-test-1", "val-test-1", 49m));
        using var scope = database.CreateScope();
        var finance = new FinanceService(
            database.ApplicationDbContextFactory,
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(),
            gateway,
            new ConfigurationBuilder().Build());

        var outcome = await finance.ProcessGatewayPaymentAsync(new GatewayCallbackValues("txn-test-1", "val-test-1"), TestToken);

        Assert.Equal(GatewayPaymentOutcome.ReviewRequired, outcome);
        await using var verify = await database.CreateContextAsync(TestToken);
        Assert.Equal(PaymentStatus.ReviewRequired, (await verify.Payments.SingleAsync(item => item.Id == paymentId, TestToken)).Status);
        Assert.Empty(await verify.PaymentAllocations.ToListAsync(TestToken));
        Assert.Empty(await verify.PaymentReceipts.ToListAsync(TestToken));
    }

    [Fact]
    public async Task DuplicateVerifiedGatewayCallbackCreatesOnlyOneAllocationAndReceipt()
    {
        await using var database = await SqliteSchoolDatabase.CreateAsync(TestToken);
        var user = await database.CreateUserAsync("payer@example.test");
        var (paymentId, _) = await SeedGatewayPaymentAsync(database, user.Id, TestToken);
        var gateway = new FakeSslCommerzGateway(ValidPayment("txn-test-1", "val-test-1", 100m));
        using var scope = database.CreateScope();
        var finance = new FinanceService(
            database.ApplicationDbContextFactory,
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(),
            gateway,
            new ConfigurationBuilder().Build());
        var callback = new GatewayCallbackValues("txn-test-1", "val-test-1");

        Assert.Equal(GatewayPaymentOutcome.Processed, await finance.ProcessGatewayPaymentAsync(callback, TestToken));
        Assert.Equal(GatewayPaymentOutcome.AlreadyProcessed, await finance.ProcessGatewayPaymentAsync(callback, TestToken));

        await using var verify = await database.CreateContextAsync(TestToken);
        Assert.Equal(PaymentStatus.Succeeded, (await verify.Payments.SingleAsync(item => item.Id == paymentId, TestToken)).Status);
        Assert.Single(await verify.PaymentAllocations.ToListAsync(TestToken));
        Assert.Single(await verify.PaymentReceipts.ToListAsync(TestToken));
        Assert.Equal(1, gateway.ValidationCalls);
    }

    private static AdmissionSubmissionInput NewApplication(int yearId, int sectionId, string studentName) => new(
        yearId,
        sectionId,
        studentName,
        new DateOnly(2015, 4, 12),
        "",
        "",
        "Guardian Name",
        "Parent",
        "+8801712345678",
        "guardian@example.test",
        "");

    private static async Task<(int PaymentId, int InvoiceId)> SeedGatewayPaymentAsync(
        SqliteSchoolDatabase database,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var db = await database.CreateContextAsync(cancellationToken);
        var year = new AcademicYear { Name = "2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var schoolClass = new SchoolClass { Name = "Grade 3", GradeLevel = 3 };
        var section = new Section { Name = "A", Capacity = 30, Class = schoolClass };
        var application = new AdmissionApplication
        {
            ApplicationNumber = "APP-TEST-PAYMENT-1",
            AcademicYear = year,
            RequestedSection = section,
            StudentFullName = "Tania Akter",
            DateOfBirth = new DateOnly(2017, 5, 8),
            GuardianFullName = "Parent Two",
            GuardianRelationship = "Parent",
            GuardianPhone = "+8801712345678",
            Status = AdmissionStatus.Approved
        };
        var student = new Student
        {
            AdmissionApplication = application,
            StudentNumber = "STU-2026-0002",
            FullName = application.StudentFullName,
            DateOfBirth = application.DateOfBirth,
            Status = StudentStatus.Active
        };
        var enrollment = new Enrollment { Student = student, AcademicYear = year, Section = section, Status = EnrollmentStatus.Active };
        var feeStructure = new FeeStructure
        {
            AcademicYear = year,
            SchoolClass = schoolClass,
            Category = FeeCategory.Tuition,
            Name = "Term fee",
            Amount = 100m,
            DueDate = new DateOnly(2026, 12, 1),
            CreatedByUserId = userId
        };
        var assignment = new StudentFeeAssignment
        {
            FeeStructure = feeStructure,
            Enrollment = enrollment,
            Amount = 100m,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedByUserId = userId
        };
        var invoice = new FeeInvoice
        {
            StudentFeeAssignment = assignment,
            Student = student,
            InvoiceNumber = "INV-TEST-0001",
            FeeName = feeStructure.Name,
            Category = feeStructure.Category,
            GrossAmount = 100m,
            TotalAmount = 100m,
            Currency = "BDT",
            DueDate = new DateOnly(2026, 12, 1),
            Status = FeeInvoiceStatus.Issued
        };
        var payment = new Payment
        {
            FeeInvoice = invoice,
            Amount = 100m,
            Currency = "BDT",
            Method = PaymentMethod.OnlineGateway,
            Status = PaymentStatus.PendingGateway,
            GatewayName = "SSLCOMMERZ",
            GatewayTransactionId = "txn-test-1",
            GatewaySessionKey = "session-test-1",
            CreatedByUserId = userId
        };

        db.AcademicYears.Add(year);
        db.Classes.Add(schoolClass);
        db.Sections.Add(section);
        db.AdmissionApplications.Add(application);
        db.Students.Add(student);
        db.Enrollments.Add(enrollment);
        db.FeeStructures.Add(feeStructure);
        db.StudentFeeAssignments.Add(assignment);
        db.FeeInvoices.Add(invoice);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return (payment.Id, invoice.Id);
    }

    private static ValidatedGatewayPayment ValidPayment(string transactionId, string validationId, decimal amount) => new(
        "VALID",
        "DONE",
        transactionId,
        validationId,
        amount,
        "BDT",
        amount,
        "bank-transaction-test",
        "0");

    private sealed class FakeSslCommerzGateway(ValidatedGatewayPayment payment) : ISslCommerzGateway
    {
        public int ValidationCalls { get; private set; }
        public bool IsConfigured => true;

        public Task<GatewaySessionResponse> CreateSessionAsync(GatewaySessionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ValidatedGatewayPayment> ValidatePaymentAsync(string validationId, CancellationToken cancellationToken = default)
        {
            ValidationCalls++;
            return Task.FromResult(payment);
        }

        public Task<GatewayRefundStartResponse> StartRefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GatewayRefundStatusResponse> GetRefundStatusAsync(string refundReference, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
