# Phase 6 — Imports, exports, and communication

Phase 6 adds roster and marks spreadsheets, fee exports, protected PDF and ZIP downloads, announcements, parent-teacher messages, audit records, and a database-backed email queue.

## 1. Student roster spreadsheet

Open **Imports and exports** from the staff navigation. Download the student template and fill the 13 columns using the exact headers. Dates must use yyyy-MM-dd; class, section, and academic year must match the configured school records. Student numbers must be unique.

Upload one .xlsx file (up to 5 MB and 500 rows). The server checks every row, guardian contact, existing student number, section capacity, and the class/year combination before writing anything. If any row has an error, no records are saved and the page lists the workbook row and reason. A valid import runs in one serializable transaction and creates the student, guardian, enrollment, approved admission application, review entry, and an audit event.

Importing a roster marks each row as admitted. Use it for a verified roster migration; new applicants should continue through the regular admission review workflow.

The student directory keeps its CSV export and adds a filtered Excel export. Accountants and Super Admins can export the finance invoice report from **Fees and finance** or **Imports and exports**.

## 2. Teacher marks workbook

From **Examination marks**, open an assigned subject and download its roster-specific template. Keep each student number and name as provided; fill the marks column. The server verifies every roster row, rejects duplicates and out-of-roster students, checks marks against full marks, and saves all marks together only when the workbook is valid. At least one mark must be present. A teacher can only generate or import a workbook for their assigned subject while it is open for entry.

## 3. PDF and ZIP downloads

- Parent invoice PDF links are shown on **Children's fees**. Invoice access is checked against the parent's currently linked children.
- Receipt PDFs are available only for successful payments. The existing receipt page can still be printed.
- Report card PDFs are generated only for published results returned by the signed-in parent's linked-child query.
- Admission staff can enter up to 50 student record IDs on **Imports and exports** to download a ZIP containing those students' admitted application documents. Files remain outside wwwroot; the download route checks staff roles and the service selects files by student record ID.
- Message attachments accept PDF, PNG, or JPEG data, validate the file signature, use an unpredictable private storage key, and are downloaded only by the conversation participants.

PDF links and private document downloads create audit events. The application does not expose an audit edit or delete screen; database operators can still change database contents.

## 4. Announcements and parent-teacher messages

Any signed-in account can read announcements for its audience. Principals and Super Admins can publish notices to **Everyone**, **Parents**, or **Staff** from **School announcements**.

Parents can start a conversation with a teacher assigned to one of their linked children's active sections. Teachers can read and reply to conversations addressed to their account. The service checks participants on every read and send operation. Attachments are private, limited to 5 MB, and restricted to PDF, PNG, or JPEG.

## 5. SMTP and background email

Emails are stored in EmailOutboxMessages first. Identity confirmation and password reset messages, plus overdue invoice reminders, use this queue. The hosted worker dispatches queued messages every 30 seconds and sweeps overdue invoices once per day. A reminder is queued once per invoice and guardian email. Failed SMTP attempts retry up to five times with increasing delays; messages remain queued while SMTP is not configured.

Set SMTP values with User Secrets for the Web project (replace all placeholders locally):

~~~~sh
dotnet user-secrets set "Email:Smtp:Host" "<smtp-host>" --project src/SchoolManagement.Web
dotnet user-secrets set "Email:Smtp:Port" "587" --project src/SchoolManagement.Web
dotnet user-secrets set "Email:Smtp:EnableSsl" "true" --project src/SchoolManagement.Web
dotnet user-secrets set "Email:Smtp:UserName" "<smtp-username>" --project src/SchoolManagement.Web
dotnet user-secrets set "Email:Smtp:Password" "<smtp-password>" --project src/SchoolManagement.Web
dotnet user-secrets set "Email:Smtp:FromAddress" "<verified-sender@example.com>" --project src/SchoolManagement.Web
dotnet user-secrets set "Email:Smtp:FromName" "School Management System" --project src/SchoolManagement.Web
~~~~

Do not commit SMTP credentials or real student and guardian data. The **Audit and background jobs** page shows recent job outcomes, but not email bodies or credentials.

## 6. Audit records and local schema

Application audit events record sensitive admission decisions, student profile changes, mark edits and publication, payment and refund actions, data imports and exports, private downloads, announcements, and messages. The audit screen is read-only and Super Admin only. Background job outcomes are recorded separately.

The Phase6ImportsExportsCommunication migration adds announcements, conversations, messages, private message attachment metadata, audit logs, email outbox messages, and background job logs. Development startup applies migrations automatically; production deployments must apply migrations explicitly after a database backup. The local `school_management` database has since been created and all six migrations applied.

## 7. PDF licensing and validation

The project uses ClosedXML 0.105.1 for .xlsx files and QuestPDF 2026.9.1 for PDF generation. QuestPDF is configured for its Community license for this MIT-licensed learning/open-source project. Its license depends on the organization that operates the application; confirm eligibility before institutional production use. Public-sector organizations that are not qualifying academic institutions are not eligible for the Community license. See the [QuestPDF license guide](https://github.com/QuestPDF/QuestPDF/blob/main/LICENSE.md) and [pricing page](https://www.questpdf.com/pricing.html).

Build the solution from the repository root:

~~~~sh
dotnet build SchoolManagement.sln
~~~~

The solution build checks compilation and Razor page generation. The Phase 2–6 browser workflows still need a manual walkthrough. SMTP delivery also needs credentials for a real mail server; no message is sent when SMTP settings are absent.
