using System.Data;
using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Administration;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Finance;
using SchoolManagement.Application.Examinations;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Entities;
using SchoolManagement.Core.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services;

public sealed partial class PhaseSixService
{
    private static readonly string[] StudentImportHeaders =
    [
        "Student Number", "Student Name", "Date of Birth", "Gender", "Student Address",
        "Guardian Name", "Relationship", "Guardian Phone", "Guardian Email", "Guardian Address",
        "Class", "Section", "Academic Year"
    ];

    private static readonly string[] MarksImportHeaders = ["Student Number", "Student Name", "Marks Obtained"];

    public async Task<PhaseSixFile> CreateMarksImportTemplateAsync(
        string teacherUserId,
        int examSubjectId,
        CancellationToken cancellationToken = default)
    {
        var register = await examinationService.GetMarkRegisterAsync(teacherUserId, examSubjectId, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Marks");
        for (var index = 0; index < MarksImportHeaders.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = MarksImportHeaders[index];
            sheet.Cell(1, index + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var student in register.Students)
        {
            sheet.Cell(row, 1).Value = student.StudentNumber;
            sheet.Cell(row, 2).Value = student.StudentName;
            if (student.MarksObtained.HasValue) sheet.Cell(row, 3).Value = student.MarksObtained.Value;
            row++;
        }
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(10, 32);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new PhaseSixFile(stream.ToArray(), $"marks-{examSubjectId}-template.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<IReadOnlyList<ImportRowError>> ImportMarksAsync(
        string teacherUserId,
        int examSubjectId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        if (content.IsEmpty || content.Length > MaximumUploadBytes)
            throw new ArgumentException("Choose an Excel workbook no larger than 5 MB.", nameof(content));
        var register = await examinationService.GetMarkRegisterAsync(teacherUserId, examSubjectId, cancellationToken);
        using var input = new MemoryStream(content.ToArray());
        using var workbook = new XLWorkbook(input);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("The workbook must contain a Marks worksheet.", nameof(content));
        for (var index = 0; index < MarksImportHeaders.Length; index++)
        {
            if (!string.Equals(sheet.Cell(1, index + 1).GetString().Trim(), MarksImportHeaders[index], StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The workbook headers do not match the marks import template.", nameof(content));
        }
        var lastRow = sheet.RangeUsed()?.RangeAddress.LastAddress.RowNumber ?? 1;
        if (lastRow > register.Students.Count + 1)
            throw new ArgumentException("The workbook contains more rows than the assigned student roster.", nameof(content));

        var students = register.Students.ToDictionary(item => item.StudentNumber, StringComparer.OrdinalIgnoreCase);
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ImportRowError>();
        var entries = new List<MarkInput>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var number = sheet.Cell(rowNumber, 1).GetString().Trim();
            var name = sheet.Cell(rowNumber, 2).GetString().Trim();
            var markText = sheet.Cell(rowNumber, 3).GetString().Trim();
            if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(markText)) continue;
            if (!seenNumbers.Add(number))
            {
                errors.Add(new ImportRowError(rowNumber, "Student number appears more than once."));
                continue;
            }
            if (!students.TryGetValue(number, out var student))
            {
                errors.Add(new ImportRowError(rowNumber, "Student number is not in this assigned subject register."));
                continue;
            }
            if (!string.Equals(name, student.StudentName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ImportRowError(rowNumber, "Student name does not match the assigned roster."));
                continue;
            }

            decimal? mark = null;
            if (!string.IsNullOrWhiteSpace(markText))
            {
                if (!decimal.TryParse(markText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    errors.Add(new ImportRowError(rowNumber, "Marks must be a number using a period for decimal places."));
                    continue;
                }
                if (parsed < 0 || parsed > register.WorkItem.FullMarks)
                {
                    errors.Add(new ImportRowError(rowNumber, $"Marks must be between 0 and {register.WorkItem.FullMarks:0.##}."));
                    continue;
                }
                mark = parsed;
            }
            entries.Add(new MarkInput(student.StudentId, mark));
        }

        var missing = register.Students.Where(student => !seenNumbers.Contains(student.StudentNumber)).ToArray();
        errors.AddRange(missing.Select(student => new ImportRowError(0, $"Roster row for {student.StudentNumber} is missing.")));
        if (errors.Count == 0 && !entries.Any(item => item.MarksObtained.HasValue))
            errors.Add(new ImportRowError(1, "Enter at least one mark before importing."));
        if (errors.Count > 0) return errors;

        await examinationService.SaveMarksAsync(teacherUserId, examSubjectId, entries, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await AddAuditAsync(db, teacherUserId, "MarksImport", "ExamSubject", examSubjectId.ToString(CultureInfo.InvariantCulture), $"Imported marks for {entries.Count} roster records.");
        await db.SaveChangesAsync(cancellationToken);
        return [];
    }

    public Task<PhaseSixFile> CreateStudentImportTemplateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Students");
        for (var index = 0; index < StudentImportHeaders.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = StudentImportHeaders[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9F2FF");
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(12, 32);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(new PhaseSixFile(stream.ToArray(), "student-import-template.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    public async Task<PhaseSixFile> ExportStudentsExcelAsync(
        StudentSearchFilter filter,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin, SchoolRoles.AdmissionOfficer);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Students.AsNoTracking();
        if (filter.Status.HasValue) query = query.Where(student => student.Status == filter.Status.Value);
        if (filter.AcademicYearId.HasValue || filter.SectionId.HasValue)
        {
            query = query.Where(student => student.Enrollments.Any(enrollment =>
                (!filter.AcademicYearId.HasValue || enrollment.AcademicYearId == filter.AcademicYearId.Value)
                && (!filter.SectionId.HasValue || enrollment.SectionId == filter.SectionId.Value)));
        }
        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(student => student.StudentNumber.Contains(search) || student.FullName.Contains(search));
        }

        var students = await query
            .Include(student => student.Guardians).ThenInclude(link => link.Guardian)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.AcademicYear)
            .Include(student => student.Enrollments).ThenInclude(enrollment => enrollment.Section)!.ThenInclude(section => section!.Class)
            .OrderBy(student => student.FullName).ThenBy(student => student.StudentNumber)
            .Take(10000)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Students");
        var headers = new[] { "Student Number", "Name", "Date of Birth", "Status", "Class", "Section", "Academic Year", "Guardian", "Guardian Phone", "Guardian Email" };
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var student in students)
        {
            var enrollment = student.Enrollments
                .Where(item => (!filter.AcademicYearId.HasValue || item.AcademicYearId == filter.AcademicYearId.Value)
                    && (!filter.SectionId.HasValue || item.SectionId == filter.SectionId.Value))
                .OrderByDescending(item => item.AcademicYear?.StartDate)
                .FirstOrDefault();
            var guardian = student.Guardians.Select(link => link.Guardian).FirstOrDefault(item => item is not null);
            sheet.Cell(row, 1).Value = student.StudentNumber;
            sheet.Cell(row, 2).Value = student.FullName;
            sheet.Cell(row, 3).Value = student.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sheet.Cell(row, 4).Value = student.Status.ToString();
            sheet.Cell(row, 5).Value = enrollment?.Section?.Class?.Name ?? "";
            sheet.Cell(row, 6).Value = enrollment?.Section?.Name ?? "";
            sheet.Cell(row, 7).Value = enrollment?.AcademicYear?.Name ?? "";
            sheet.Cell(row, 8).Value = guardian?.FullName ?? "";
            sheet.Cell(row, 9).Value = guardian?.Phone ?? "";
            sheet.Cell(row, 10).Value = guardian?.Email ?? "";
            row++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(10, 36);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await AddExportAuditAsync(actorUserId, "StudentExcelExport", students.Count, cancellationToken);
        return new PhaseSixFile(stream.ToArray(), "students.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<StudentImportResult> ImportStudentsAsync(
        string actorUserId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin, SchoolRoles.AdmissionOfficer);
        if (content.IsEmpty || content.Length > MaximumUploadBytes)
        {
            throw new ArgumentException("Choose an Excel workbook no larger than 5 MB.", nameof(content));
        }

        using var input = new MemoryStream(content.ToArray());
        using var workbook = new XLWorkbook(input);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("The workbook must contain a Students worksheet.", nameof(content));
        for (var index = 0; index < StudentImportHeaders.Length; index++)
        {
            if (!string.Equals(sheet.Cell(1, index + 1).GetString().Trim(), StudentImportHeaders[index], StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The workbook headers do not match the student import template.", nameof(content));
            }
        }

        var used = sheet.RangeUsed();
        var lastRow = used?.RangeAddress.LastAddress.RowNumber ?? 1;
        if (lastRow > 501)
        {
            throw new ArgumentException("Import at most 500 student rows per workbook.", nameof(content));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var years = await db.AcademicYears.AsNoTracking().ToListAsync(cancellationToken);
        var sections = await db.Sections.AsNoTracking().Include(section => section.Class).ToListAsync(cancellationToken);
        var existingNumbers = await db.Students.AsNoTracking().Select(student => student.StudentNumber).ToListAsync(cancellationToken);
        var currentCounts = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.Status == EnrollmentStatus.Active)
            .GroupBy(enrollment => new { enrollment.SectionId, enrollment.AcademicYearId })
            .Select(group => new { group.Key.SectionId, group.Key.AcademicYearId, Count = group.Count() })
            .ToDictionaryAsync(item => (item.SectionId, item.AcademicYearId), item => item.Count, cancellationToken);
        var seenNumbers = new HashSet<string>(existingNumbers, StringComparer.OrdinalIgnoreCase);
        var capacityAdds = new Dictionary<(int SectionId, int YearId), int>();
        var rows = new List<ImportStudentRow>();
        var errors = new List<ImportRowError>();

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = Enumerable.Range(1, StudentImportHeaders.Length).Select(column => sheet.Cell(rowNumber, column).GetString().Trim()).ToArray();
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            var rowErrors = new List<string>();
            var number = values[0];
            var name = values[1];
            if (number.Length is 0 or > 32) rowErrors.Add("Student number is required and must be at most 32 characters.");
            else if (!seenNumbers.Add(number)) rowErrors.Add("Student number already exists or is duplicated in this workbook.");
            if (name.Length is 0 or > 160) rowErrors.Add("Student name is required and must be at most 160 characters.");
            if (!DateOnly.TryParseExact(values[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOfBirth)
                || dateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                rowErrors.Add("Date of birth must use yyyy-MM-dd and be in the past.");
            }
            if (values[3].Length > 40) rowErrors.Add("Gender must be at most 40 characters.");
            if (values[4].Length > 500 || values[9].Length > 500) rowErrors.Add("Addresses must be at most 500 characters.");
            if (values[5].Length is 0 or > 160 || values[6].Length is 0 or > 60) rowErrors.Add("Guardian name and relationship are required and must fit their field limits.");
            if (values[7].Length is 0 or > 40 || !new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(values[7])) rowErrors.Add("Enter a valid guardian phone number.");
            if (values[8].Length > 254 || (!string.IsNullOrWhiteSpace(values[8]) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(values[8]))) rowErrors.Add("Enter a valid guardian email address.");

            var year = years.FirstOrDefault(item => string.Equals(item.Name, values[12], StringComparison.OrdinalIgnoreCase));
            if (year is null) rowErrors.Add("Academic year was not found.");
            var section = sections.FirstOrDefault(item =>
                string.Equals(item.Name, values[11], StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Class?.Name, values[10], StringComparison.OrdinalIgnoreCase));
            if (section is null) rowErrors.Add("Class and section were not found.");
            else if (year is not null)
            {
                var key = (section.Id, year.Id);
                var nextCount = currentCounts.GetValueOrDefault(key) + capacityAdds.GetValueOrDefault(key) + 1;
                if (section.Capacity > 0 && nextCount > section.Capacity) rowErrors.Add("The class section does not have enough capacity for this row.");
                if (rowErrors.Count == 0) capacityAdds[key] = capacityAdds.GetValueOrDefault(key) + 1;
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors.Select(message => new ImportRowError(rowNumber, message)));
                continue;
            }

            rows.Add(new ImportStudentRow(rowNumber, number, name, dateOfBirth, values[3], values[4], values[5], values[6], values[7], values[8], values[9], section!, year!));
        }

        if (errors.Count > 0) return new StudentImportResult(0, errors);
        if (rows.Count == 0) return new StudentImportResult(0, [new ImportRowError(1, "The workbook contains no student rows.")]);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var capacityErrors = new List<ImportRowError>();
        foreach (var group in rows.GroupBy(row => (SectionId: row.Section.Id, AcademicYearId: row.AcademicYear.Id)))
        {
            var section = group.First().Section;
            if (section.Capacity <= 0) continue;

            var enrolledCount = await db.Enrollments.CountAsync(enrollment =>
                enrollment.SectionId == section.Id
                && enrollment.AcademicYearId == group.Key.AcademicYearId
                && enrollment.Status == EnrollmentStatus.Active, cancellationToken);
            var availableSeats = Math.Max(section.Capacity - enrolledCount, 0);
            capacityErrors.AddRange(group.OrderBy(row => row.RowNumber).Skip(availableSeats)
                .Select(row => new ImportRowError(row.RowNumber, "The class section filled before this import could be saved.")));
        }

        if (capacityErrors.Count > 0) return new StudentImportResult(0, capacityErrors);

        var now = DateTimeOffset.UtcNow;
        var batchReference = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        foreach (var row in rows)
        {
            var application = new AdmissionApplication
            {
                ApplicationNumber = $"IMP{now:yyMMddHHmmss}{batchReference}{row.RowNumber:D4}",
                AcademicYearId = row.AcademicYear.Id,
                RequestedSectionId = row.Section.Id,
                StudentFullName = row.Name,
                DateOfBirth = row.DateOfBirth,
                Gender = row.Gender,
                StudentAddress = row.Address,
                GuardianFullName = row.GuardianName,
                GuardianRelationship = row.Relationship,
                GuardianPhone = row.Phone,
                GuardianEmail = row.Email,
                GuardianAddress = row.GuardianAddress,
                Status = AdmissionStatus.Approved,
                SubmittedAtUtc = now
            };
            var student = new Student
            {
                AdmissionApplication = application,
                StudentNumber = row.StudentNumber,
                FullName = row.Name,
                DateOfBirth = row.DateOfBirth,
                Gender = row.Gender,
                Address = row.Address,
                Status = StudentStatus.Active,
                AdmittedAtUtc = now
            };
            var guardian = new Guardian
            {
                FullName = row.GuardianName,
                Relationship = row.Relationship,
                Phone = row.Phone,
                Email = row.Email,
                Address = row.GuardianAddress
            };
            db.AdmissionApplications.Add(application);
            db.Students.Add(student);
            db.Guardians.Add(guardian);
            db.StudentGuardians.Add(new StudentGuardian { Student = student, Guardian = guardian });
            db.Enrollments.Add(new Enrollment
            {
                Student = student,
                AcademicYearId = row.AcademicYear.Id,
                SectionId = row.Section.Id,
                Status = EnrollmentStatus.Active,
                EnrolledAtUtc = now
            });
            db.AdmissionReviews.Add(new AdmissionReview
            {
                AdmissionApplication = application,
                ReviewerUserId = actorUserId,
                Decision = AdmissionDecision.Approved,
                Remarks = "Approved through validated student roster import.",
                ReviewedAtUtc = now
            });
        }

        await AddAuditAsync(db, actorUserId, "StudentImport", "Student", "bulk", $"Imported {rows.Count} roster records.");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StudentImportResult(rows.Count, []);
    }

    public async Task<PhaseSixFile> ExportFinanceReportExcelAsync(string actorUserId, CancellationToken cancellationToken = default)
    {
        var dashboard = await financeService.GetDashboardAsync(actorUserId, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Invoices");
        var headers = new[] { "Invoice", "Student number", "Student", "Academic year", "Class", "Section", "Fee", "Category", "Gross", "Discount", "Total", "Received", "Refunded", "Balance", "Currency", "Due date", "Status", "Overdue" };
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Cell(1, index + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var invoice in dashboard.Invoices.OrderBy(item => item.DueDate).ThenBy(item => item.InvoiceNumber))
        {
            object?[] values = [invoice.InvoiceNumber, invoice.StudentNumber, invoice.StudentName, invoice.AcademicYearName, invoice.ClassName, invoice.SectionName, invoice.FeeName, invoice.Category.ToString(), invoice.GrossAmount, invoice.DiscountAmount, invoice.TotalAmount, invoice.AmountReceived, invoice.AmountRefunded, invoice.Balance, invoice.Currency, invoice.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), invoice.Status.ToString(), invoice.IsOverdue ? "Yes" : "No"];
            for (var column = 0; column < values.Length; column++) sheet.Cell(row, column + 1).Value = XLCellValue.FromObject(values[column]);
            row++;
        }
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(10, 34);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await AddExportAuditAsync(actorUserId, "FinanceExcelExport", dashboard.Invoices.Count, cancellationToken);
        return new PhaseSixFile(stream.ToArray(), "finance-invoices.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<PhaseSixFile> RenderInvoicePdfAsync(string actorUserId, int invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin, SchoolRoles.Accountant, SchoolRoles.ParentStudent);
        var user = await userManager.FindByIdAsync(actorUserId) ?? throw new UnauthorizedAccessException();
        var isStaff = await userManager.IsInRoleAsync(user, SchoolRoles.SuperAdmin) || await userManager.IsInRoleAsync(user, SchoolRoles.Accountant);
        var invoices = isStaff
            ? (await financeService.GetDashboardAsync(actorUserId, cancellationToken)).Invoices
            : await financeService.GetMyInvoicesAsync(actorUserId, cancellationToken);
        var invoice = invoices.SingleOrDefault(item => item.Id == invoiceId)
            ?? throw new KeyNotFoundException("The invoice is unavailable for this account.");

        var pdf = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(header =>
            {
                header.Item().Text("SCHOOL MANAGEMENT SYSTEM").SemiBold().FontSize(12).FontColor(Colors.Blue.Darken2);
                header.Item().Text("Fee invoice").Bold().FontSize(22);
            });
            page.Content().PaddingVertical(18).Column(column =>
            {
                column.Spacing(7);
                column.Item().Text($"Invoice: {invoice.InvoiceNumber}").SemiBold();
                column.Item().Text($"Student: {invoice.StudentName} ({invoice.StudentNumber})");
                column.Item().Text($"Class / section: {invoice.ClassName} / {invoice.SectionName}");
                column.Item().Text($"Academic year: {invoice.AcademicYearName}");
                column.Item().Text($"Fee: {invoice.FeeName} · {invoice.Category}");
                column.Item().Text($"Due date: {invoice.DueDate:yyyy-MM-dd} · Status: {invoice.Status}");
                column.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(3); columns.RelativeColumn(); });
                    table.Header(header => { header.Cell().Element(PdfHeaderCell).Text("Description"); header.Cell().Element(PdfHeaderCell).AlignRight().Text("Amount"); });
                    AddPdfRow(table, "Gross amount", $"{invoice.GrossAmount:N2} {invoice.Currency}");
                    AddPdfRow(table, "Discount", $"− {invoice.DiscountAmount:N2} {invoice.Currency}");
                    AddPdfRow(table, "Invoice total", $"{invoice.TotalAmount:N2} {invoice.Currency}");
                    AddPdfRow(table, "Payments received", $"{invoice.AmountReceived:N2} {invoice.Currency}");
                    AddPdfRow(table, "Refunds", $"{invoice.AmountRefunded:N2} {invoice.Currency}");
                    AddPdfRow(table, "Outstanding balance", $"{invoice.Balance:N2} {invoice.Currency}");
                });
            });
            page.Footer().AlignCenter().Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
        await AddExportAuditAsync(actorUserId, "InvoicePdfDownload", 1, cancellationToken, invoiceId.ToString(CultureInfo.InvariantCulture));
        return new PhaseSixFile(pdf, $"invoice-{invoice.InvoiceNumber}.pdf", "application/pdf");
    }

    public async Task<PhaseSixFile> RenderReceiptPdfAsync(string actorUserId, int paymentId, CancellationToken cancellationToken = default)
    {
        var receipt = await financeService.GetReceiptAsync(actorUserId, paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Only verified successful payments have downloadable receipts.");
        var pdf = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(style => style.FontSize(11));
            page.Header().Column(header =>
            {
                header.Item().Text("SCHOOL MANAGEMENT SYSTEM").SemiBold().FontSize(12).FontColor(Colors.Blue.Darken2);
                header.Item().Text("Payment receipt").Bold().FontSize(22);
            });
            page.Content().PaddingVertical(18).Column(column =>
            {
                column.Spacing(9);
                column.Item().Text($"Receipt: {receipt.ReceiptNumber}").SemiBold();
                column.Item().Text($"Student: {receipt.StudentName} ({receipt.StudentNumber})");
                column.Item().Text($"Invoice: {receipt.InvoiceNumber} · {receipt.FeeName}");
                column.Item().Text($"Academic year: {receipt.AcademicYearName}");
                column.Item().Text($"Payment date: {receipt.PaidAtUtc:yyyy-MM-dd HH:mm} UTC");
                column.Item().Text($"Method: {receipt.Method} · Reference: {receipt.Reference}");
                column.Item().PaddingTop(16).BorderTop(1).BorderBottom(1).PaddingVertical(12)
                    .Row(row => { row.RelativeItem().Text("Amount received").SemiBold(); row.AutoItem().Text($"{receipt.Amount:N2} {receipt.Currency}").Bold().FontSize(16); });
                column.Item().PaddingTop(12).Text("This receipt confirms a successful payment recorded against the invoice above.").FontColor(Colors.Grey.Darken2);
            });
            page.Footer().AlignCenter().Text($"Issued {receipt.IssuedAtUtc:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
        await AddExportAuditAsync(actorUserId, "ReceiptPdfDownload", 1, cancellationToken, paymentId.ToString(CultureInfo.InvariantCulture));
        return new PhaseSixFile(pdf, $"receipt-{receipt.ReceiptNumber}.pdf", "application/pdf");
    }

    public async Task<PhaseSixFile> RenderReportCardPdfAsync(string actorUserId, int studentId, int examinationId, CancellationToken cancellationToken = default)
    {
        var cards = await examinationService.GetMyReportCardsAsync(actorUserId, cancellationToken);
        var card = cards.SingleOrDefault(item => item.StudentId == studentId && item.ExaminationId == examinationId)
            ?? throw new KeyNotFoundException("This published report card is unavailable for the linked children.");
        var pdf = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(header =>
            {
                header.Item().Text("SCHOOL MANAGEMENT SYSTEM").SemiBold().FontSize(12).FontColor(Colors.Blue.Darken2);
                header.Item().Text("Published report card").Bold().FontSize(21);
            });
            page.Content().PaddingVertical(15).Column(column =>
            {
                column.Spacing(6);
                column.Item().Text($"Student: {card.StudentName} ({card.StudentNumber})").SemiBold();
                column.Item().Text($"Examination: {card.ExaminationName} · {card.TermName} · {card.AcademicYearName}");
                column.Item().Text($"Total: {card.TotalMarks:0.##} / {card.FullMarks:0.##} · {card.OverallPercentage:0.##}% · GPA {card.Gpa:0.00} · {(card.IsPassing ? "Pass" : "Review required")}").SemiBold();
                column.Item().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(3); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                    table.Header(header =>
                    {
                        header.Cell().Element(PdfHeaderCell).Text("Class / section");
                        header.Cell().Element(PdfHeaderCell).Text("Subject");
                        header.Cell().Element(PdfHeaderCell).AlignRight().Text("Marks");
                        header.Cell().Element(PdfHeaderCell).AlignRight().Text("Grade");
                        header.Cell().Element(PdfHeaderCell).AlignRight().Text("Result");
                    });
                    foreach (var subject in card.Subjects)
                    {
                        table.Cell().Element(PdfBodyCell).Text($"{subject.ClassName} / {subject.SectionName}");
                        table.Cell().Element(PdfBodyCell).Text(subject.SubjectName);
                        table.Cell().Element(PdfBodyCell).AlignRight().Text($"{subject.MarksObtained:0.##} / {subject.FullMarks:0.##}");
                        table.Cell().Element(PdfBodyCell).AlignRight().Text($"{subject.Grade.LetterGrade} ({subject.Grade.GradePoint:0.00})");
                        table.Cell().Element(PdfBodyCell).AlignRight().Text(subject.Grade.IsPassing ? "Pass" : "Review");
                    }
                });
            });
            page.Footer().AlignCenter().Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
        await AddExportAuditAsync(actorUserId, "ReportCardPdfDownload", 1, cancellationToken, $"{studentId}:{examinationId}");
        return new PhaseSixFile(pdf, $"report-card-{card.StudentNumber}-{card.ExaminationId}.pdf", "application/pdf");
    }

    public async Task<PhaseSixFile> ExportStudentDocumentsZipAsync(
        string actorUserId,
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(actorUserId, cancellationToken, SchoolRoles.SuperAdmin, SchoolRoles.AdmissionOfficer);
        var ids = studentIds.Where(id => id > 0).Distinct().Take(51).ToArray();
        if (ids.Length is 0 or > 50) throw new ArgumentException("Select between 1 and 50 students for a document ZIP.", nameof(studentIds));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var documents = await db.ApplicationDocuments.AsNoTracking()
            .Where(document => document.AdmissionApplication!.Student != null && ids.Contains(document.AdmissionApplication.Student.Id))
            .Include(document => document.AdmissionApplication)
            .ThenInclude(application => application!.Student)
            .OrderBy(document => document.AdmissionApplication!.Student!.StudentNumber)
            .ThenBy(document => document.Id)
            .ToListAsync(cancellationToken);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var source = await documentStore.OpenReadAsync(document.StorageKey, cancellationToken);
                var studentNumber = document.AdmissionApplication?.Student?.StudentNumber ?? "student";
                var entryName = $"{SafeFileName(studentNumber)}/{SafeFileName(document.OriginalFileName)}";
                var directory = Path.GetDirectoryName(entryName)?.Replace('\\', '/') is { Length: > 0 } path
                    ? path + "/"
                    : "";
                var baseName = Path.GetFileName(entryName);
                var suffix = 1;
                while (!usedNames.Add(entryName))
                {
                    var extension = Path.GetExtension(baseName);
                    entryName = $"{directory}{Path.GetFileNameWithoutExtension(baseName)}-{suffix++}{extension}";
                }
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var target = entry.Open();
                await source.CopyToAsync(target, cancellationToken);
            }
        }

        await AddExportAuditAsync(actorUserId, "StudentDocumentZipDownload", documents.Count, cancellationToken, $"students={ids.Length}");
        return new PhaseSixFile(output.ToArray(), "student-documents.zip", "application/zip");
    }

    private async Task AddExportAuditAsync(string actorUserId, string action, int itemCount, CancellationToken cancellationToken, string entityId = "")
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await AddAuditAsync(db, actorUserId, action, "Export", entityId, $"Exported {itemCount} record(s).");
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddPdfRow(TableDescriptor table, string label, string amount)
    {
        table.Cell().Element(PdfBodyCell).Text(label);
        table.Cell().Element(PdfBodyCell).AlignRight().Text(amount);
    }

    private static IContainer PdfHeaderCell(IContainer container) => container.Background(Colors.Grey.Lighten3).PaddingVertical(6).PaddingHorizontal(5);

    private static IContainer PdfBodyCell(IContainer container) => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(5);

    private sealed record ImportStudentRow(
        int RowNumber,
        string StudentNumber,
        string Name,
        DateOnly DateOfBirth,
        string Gender,
        string Address,
        string GuardianName,
        string Relationship,
        string Phone,
        string Email,
        string GuardianAddress,
        SchoolManagement.Core.Entities.Section Section,
        SchoolManagement.Core.Entities.AcademicYear AcademicYear);
}
