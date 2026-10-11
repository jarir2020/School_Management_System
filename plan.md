# School Administration & Fee Management System

Tech stack: ASP.NET Core + Blazor + MySQL

Build a school management platform where administrators manage students and teachers, teachers handle attendance and examinations, parents monitor academic progress and pay fees, and accountants manage school finances.

The goal is to build one realistic application that covers CRUD, relational databases, authentication, RBAC, approval workflows, calculations, Excel/PDF processing, file uploads, email, messaging, and audit logging.

## 1. Technology architecture

| Layer                | Technology                                                          |
| -------------------- | ------------------------------------------------------------------- |
| Programming language | C#                                                                  |
| Platform             | .NET / ASP.NET Core                                                 |
| Frontend             | Blazor Web App                                                      |
| UI interaction       | Blazor Interactive Server initially                                 |
| Database             | MySQL                                                               |
| ORM                  | Entity Framework Core + Pomelo MySQL provider                       |
| Authentication       | ASP.NET Core Identity                                               |
| Authorization        | Role-based and policy-based authorization                           |
| Excel import/export  | ClosedXML                                                           |
| PDF generation       | QuestPDF, subject to its licensing terms                            |
| Email                | MailKit or ASP.NET Core email integration                           |
| Payment gateway      | Sandbox-supported gateway, integrated through a server-side service |
| Background jobs      | .NET hosted services                                                |
| Testing              | xUnit, integration tests, and Playwright                            |

Architecture recommendation: Start with a modular monolith, not microservices. Keep admissions, academics, fees, and messaging logically separated in the same application. You can introduce separate API endpoints later if you add a mobile application.

## 2. User roles and permissions

Design the system around six roles. A single person may have multiple roles, but permissions must always be checked on the server.

| Role              | Main responsibilities                                            |
| ----------------- | ---------------------------------------------------------------- |
| Super Admin       | School settings, user accounts, academic years, all permissions  |
| Admission Officer | Applications, document verification, admission approval          |
| Teacher           | Assigned classes, attendance, marks entry, result submission     |
| Accountant        | Fee structures, invoices, payments, discounts, financial reports |
| Parent/Student    | View academic records, invoices, payments, notices               |
| Principal         | Admission decisions, result approval, academic reports           |

For an initial MVP, you can combine Parent and Student into one portal, and let the Super Admin handle principal-level approvals until you implement the full permission structure.

### Authorization rules

Examples:

- A teacher can enter marks only for assigned classes and subjects.
- An accountant can record payments but cannot modify examination marks.
- A parent can see only their linked children's records.
- A principal can approve submitted results but cannot silently change the original marks.
- An admission officer cannot grant themselves principal permissions.

Use ASP.NET Core Identity for authentication and policy-based authorization for these rules.

## 3. Core modules

[Modern School Management System Dashboard UI Design by Riasul Islam on Dribbble](https://images.openai.com/static-rsc-4/bnHUL2uHACHIY9pA3X2DdhgcpK7ehAAZc73-oKYGt71fisKqSyhFpHT0HD4t6GC6mOxF-w83r16VFoNNhtpiYhuPN9Us3o7qmayozwCF-V7XKO7ZYksqIsPt3fNhAJeOFt6qA1iLDar0xPdZ9qkXFeCbIBAgKCzfWLphRz4Udf8?purpose=inline)

Module A — School Setup

- School profile, logo, contact details
- Academic years and terms/semesters
- Classes, sections, subjects
- Teacher-subject and teacher-class assignments
- Grading scales, school calendar, holidays

Covers relational CRUD, configuration, and reusable master data.

[Panduan GRHM | Wiki Provinsi Gorontalo](https://images.openai.com/static-rsc-4/BV--eN-k0jXGfDVIh0L7EgQPwasjI6R4P94fQIT-sP2T2ARvpuc_KxBh4bJ0SwkrxGWB_hMUBQDQ_2y8VKzHFf3rOrRSaOsnPEQH2UZv1RXKM5BT39LWhaB8fu4rpRypGBsLeiuDP2L8CMefnCn0V5ZOGLCPZaW1wZddtWf7tjQ?purpose=inline)

Module B — Admissions & Student Management

- Online admission application
- Student and guardian information
- Photograph, birth certificate, and document uploads
- Document verification and approval
- Student ID generation
- Class and section enrollment
- Student promotion and transfer

Covers file validation, approval workflows, CRUD, and data relationships.

[06. Багшийн журнал-Ирцийн бүртгэл, багшийн тэмдэглэл](https://images.openai.com/static-rsc-4/GNkomf9tb8larTCrXPEDRkv13hBC87jCmzHdtjCdQK1uPrW9ke-Dk-oIiZAY31v3jNlj4y0ygD5Lu1tp--mrZBN6YG6DQsbYyA9qFD8uEM1WXhoHtAtHyiHdYBdqHHsIUzJU_GF7Nbf4U0apyih8sbzCGeQduvY2Rfp8KcZ1J2E?purpose=inline)

Module C — Teachers, Classes & Attendance

- Teacher profiles and assignments
- Daily student attendance
- Present, absent, late, and excused statuses
- Attendance correction requests
- Monthly attendance reports
- Parent notifications for absences

Covers bulk updates, relational queries, date-based logic, and reports.

[Marks Register       |    Aorasoft HelpDesk](https://images.openai.com/static-rsc-4/80539mIVcBUMUjx1vd7FKvlujAO5F9OnZuKmlO6DTmGH43vQJAd5O8gtTDTqDrfCkOnHb7S4hB-fr1WhKZ9FLw99iDgOcD-ueP3TAdY1r-GnoemXuMM6oRt4MEFRMZoL3xL75qN0oTMMl4sVLcLpU7bPlh5gVzltFbPg9kRzmuI?purpose=inline)

Module D — Examinations & Results

- Exams, subjects, full marks, pass marks
- Teacher marks entry
- Marks validation and submission
- Principal approval
- GPA/grade calculations
- Report cards and merit lists
- Result correction and revision history

Covers business mathematics, multilevel approval, PDF reports, and audit history.

[Fees & Finance Dashboard from the GLearn School Management SaaS by Deluwar Rahi  on Dribbble](https://images.openai.com/static-rsc-4/U4GCRR3d_zc_r70K4LLYOgsIWPDEHO6NYd92Il8ndh2YlSQD1zfgioBIaiG6AEjIzEEwTrdJWFikSOGH9oRfMZWw38FFODZGtUmH7tLYqcRmXSYtFeRPNMgDe_dY9fVpw2efjc7cPKIbdUovRnxe3AoY4vs0Dx9cQH7_Peiem7Q?purpose=inline)

Module E — Fees & Finance

- Class-wise tuition fee structures
- Admission fees, examination fees, and other charges
- Scholarships and discounts
- Invoice generation
- Cash, bank, and online payments
- Partial payments and outstanding balances
- Overdue tracking, receipts, refunds, financial reports

Covers calculations, payment integrations, PDF invoices, Excel exports, and financial audit logs.

[Klapp | Simple communication, that works!](https://images.openai.com/static-rsc-4/LZOVYwjZZHfvrkmWaBvCI1SADJumB67TNsDY9YXj6bwBRmqWCJygAfDmaB0G72Exzo9Lbwq1bxNKx-kzlISj_l-a8i9IG5q3A5cMsxLGyW2W1JQXcfeWriJfsiwSmSrv7pZkRQ_H2g2DMDByd5shpFMBv6PipuBbymZ2hn78Gf4?purpose=inline)

Module F — Communication & Administration

- School announcements and notices
- Parent-teacher messaging
- SMTP emails and payment reminders
- Message attachments
- Access logs and audit logs
- Data import/export and system reports

Covers messaging, email, file handling, background jobs, and audit trails.

## 4. Database design

Use MySQL with InnoDB, foreign keys, unique constraints, and transactions.

### A. Authentication and school structure

| Table                | Important fields                                     |
| -------------------- | ---------------------------------------------------- |
| `AspNetUsers`        | Identity user data                                   |
| `AspNetRoles`        | Role definitions                                     |
| `UserProfiles`       | User ID, name, phone, profile photo                  |
| `AcademicYears`      | Name, start date, end date, active status            |
| `Terms`              | Academic year ID, name, dates                        |
| `Classes`            | Name, grade level                                    |
| `Sections`           | Class ID, section name, capacity                     |
| `Subjects`           | Name, code, full marks                               |
| `TeacherAssignments` | Teacher ID, section ID, subject ID, academic year ID |

ASP.NET Core Identity creates its own tables. Configure the Identity entities to use MySQL-compatible types and migrations.

### B. Admissions and students

| Table                   | Important fields                               |
| ----------------------- | ---------------------------------------------- |
| `AdmissionApplications` | Application number, applicant data, status     |
| `ApplicationDocuments`  | Application ID, file path, document type       |
| `Students`              | Student number, name, DOB, status              |
| `Guardians`             | Name, phone, email, address                    |
| `StudentGuardians`      | Student ID, guardian ID, relationship          |
| `Enrollments`           | Student ID, section ID, academic year ID       |
| `AdmissionReviews`      | Application ID, reviewer ID, decision, remarks |
| `StudentStatusHistory`  | Student ID, previous status, new status        |

A student should have a separate enrollment record for each academic year. This preserves their history when promoted to a new class.

### C. Attendance and examinations

| Table                | Important fields                               |
| -------------------- | ---------------------------------------------- |
| `AttendanceSessions` | Section ID, subject/date, teacher ID           |
| `AttendanceRecords`  | Session ID, student ID, status                 |
| `Examinations`       | Academic year, term, name, status              |
| `ExamSubjects`       | Exam ID, subject ID, full marks, pass marks    |
| `Marks`              | Exam subject ID, student ID, marks, entered by |
| `ResultApprovals`    | Exam ID, approver ID, decision, remarks        |
| `ResultRevisions`    | Mark ID, previous value, new value, reason     |

Add a unique constraint on `(SessionId, StudentId)` for attendance and `(ExamSubjectId, StudentId)` for marks. This prevents duplicate records for the same student and session or exam subject.

### D. Fees and payments

| Table                   | Important fields                                          |
| ----------------------- | --------------------------------------------------------- |
| `FeeStructures`         | Class ID, fee type, amount, effective period              |
| `StudentFeeAssignments` | Student ID, fee structure ID, amount override             |
| `Invoices`              | Student ID, invoice number, issue date, due date, status  |
| `InvoiceItems`          | Invoice ID, description, quantity, unit price             |
| `Discounts`             | Type, value, eligibility rules                            |
| `StudentDiscounts`      | Student ID, discount ID, approval status                  |
| `Payments`              | Student ID, amount, method, transaction reference, status |
| `PaymentAllocations`    | Payment ID, invoice ID, allocated amount                  |
| `Refunds`               | Payment ID, amount, reason, status                        |

Keep payments separate from invoices. One payment may cover multiple invoices, and an invoice may be settled by multiple payments.

### E. Communication and audit

| Table                | Important fields                                   |
| -------------------- | -------------------------------------------------- |
| `Conversations`      | Student/guardian/teacher participants              |
| `Messages`           | Conversation ID, sender ID, message, sent time     |
| `MessageAttachments` | Message ID, stored file path                       |
| `Announcements`      | Title, content, audience, publish date             |
| `Notifications`      | User ID, type, message, read status                |
| `AuditLogs`          | User ID, action, entity type, entity ID, timestamp |
| `BackgroundJobLogs`  | Job type, status, attempts, error details          |

Use a database diagram before implementing the modules. The relationships between students, guardians, enrollments, invoices, payments, and examinations are especially important.

## 5. Detailed business workflows

### Workflow 1 — Student admission

#### 01

Application submitted

Student/guardian submits details and documents.

#### 02

Document verification

Admission officer verifies information.

#### 03

Approval decision

Principal or authorized officer approves or rejects.

#### 04

Student registration

System generates a unique student number.

#### 05

Enrollment

Student is assigned to a class and section.

#### 06

Fee invoice generated

Applicable admission and tuition charges are created.

Important rules:

- Rejected applications cannot be enrolled.
- Student numbers must be unique.
- The system must prevent enrollment into a full section.
- Student creation and enrollment should be atomic where appropriate.
- Admission approval should record the reviewer, timestamp, and remarks.
- Retrying a completed approval must not generate duplicate students or invoices.

### Workflow 2 — Examination and result approval

1. Admin creates an examination and assigns subjects.
2. Teachers enter marks only for their assigned subjects and students.
3. The system validates marks against each subject's full marks.
4. Teachers submit the marks for review.
5. The principal approves or rejects the submission.
6. Approved results become visible to parents and students.
7. Corrections require an authorized revision with a reason.

Suggested status transitions:

`Draft → In Progress → Submitted → Approved → Published`

Rejected submissions return to a correctable state. Published results should not be overwritten silently.

### Workflow 3 — Monthly fee collection

1. Accountant configures the fee structure.
2. The system generates monthly invoices for eligible enrollments.
3. Scholarships or approved discounts are applied.
4. Parents view invoices and outstanding balances.
5. Payment is recorded or initiated through a gateway.
6. The system verifies payment and allocates it to invoices.
7. A receipt is generated and an email is sent.
8. Overdue invoices appear in reports and trigger reminders.

Use a payment status such as `Pending`, `Succeeded`, `Failed`, or `Refunded`. An invoice should have its own status, such as `Unpaid`, `PartiallyPaid`, `Paid`, or `Overdue`.

## 6. Business calculations

These calculations should live in dedicated C# services, not inside Blazor components or directly inside database entities.

### Tuition fee calculation

Suppose a student's monthly tuition is ৳3,000, with a 20% scholarship and a ৳100 service charge.

\\[ \begin{aligned} \text{Tuition discount} &= 3000 \times 0.20 = 600\\\ \text{Total due} &= 3000 - 600 + 100\\\ &= \text{৳2,500} \end{aligned} \\]

Use `decimal` for monetary calculations, never `float` or `double`.

### Outstanding balance

\\[ \text{Outstanding} = \text{Invoice Total} - \text{Allocated Payments} \\]

Example: an invoice of ৳2,500 with ৳1,500 allocated has an outstanding balance of ৳1,000.

Do not count pending or failed transactions as successful payments.

### Overdue calculation

An invoice is overdue when its due date has passed and its outstanding balance is greater than zero. Store the due date and apply a consistent school timezone.

Late fees, if required, should be configurable rather than hard-coded.

### Result calculation

For a subject:

\\[ \text{Percentage} = \frac{\text{Marks Obtained}}{\text{Full Marks}}\times100 \\]

For a GPA-based system, use a configurable grading scale. Do not assume every school uses the same grading thresholds or GPA aggregation formula.

## 7. PDF, Excel, upload, and download features

| Feature             | Implementation                                                  |
| ------------------- | --------------------------------------------------------------- |
| Student import      | Excel template with validation and row-level errors             |
| Student export      | Filtered student list to Excel                                  |
| Marks import        | Teacher-authorized spreadsheet with subject and exam validation |
| Fee reports         | Excel export filtered by class, date, and payment status        |
| Admission documents | Private file storage with access checks                         |
| Student ID card     | PDF with photo, student number, and class                       |
| Report card         | PDF with subjects, marks, grades, and approval status           |
| Fee invoice         | PDF with invoice items, discounts, payments, and balance        |
| Payment receipt     | PDF generated only from verified successful payments            |
| ZIP export          | Authorized bulk export of selected student documents            |
| File download       | Server-side authorization before streaming the file             |

For Excel imports, validate each row before committing. Return errors such as "Student ID not found" or "Marks exceed full marks" with the relevant row number.

Never trust the original filename, extension, or client-provided MIME type for uploaded files. Store private documents outside the publicly served web root and download them through authorized endpoints.

## 8. Blazor application structure

For the initial version, use a Blazor Web App with Interactive Server rendering. It simplifies authentication and UI development while you learn the .NET ecosystem.

An example solution structure:

```
SchoolManagement/
├── SchoolManagement.sln
├── SchoolManagement.Web/
│   ├── Components/
│   │   ├── Layout/
│   │   ├── Pages/
│   │   │   ├── Dashboard/
│   │   │   ├── Admissions/
│   │   │   ├── Students/
│   │   │   ├── Teachers/
│   │   │   ├── Attendance/
│   │   │   ├── Examinations/
│   │   │   ├── Fees/
│   │   │   └── Reports/
│   │   └── Shared/
│   ├── Services/
│   └── Program.cs
├── SchoolManagement.Core/
│   ├── Entities/
│   ├── Enums/
│   └── BusinessRules/
├── SchoolManagement.Application/
│   ├── DTOs/
│   ├── Interfaces/
│   ├── Validators/
│   └── Services/
├── SchoolManagement.Infrastructure/
│   ├── Persistence/
│   ├── Configurations/
│   ├── Repositories/
│   ├── Email/
│   ├── Files/
│   └── Payments/
└── SchoolManagement.Tests/
```

This separation is useful for learning, but you do not need to introduce generic repositories or elaborate abstraction layers everywhere. EF Core's `DbContext` already provides many repository/unit-of-work capabilities.

## 9. Implementation roadmap

Build each phase to a working, testable state before moving to the next one.

Phase 1 — Foundation

Priority 1

- Create the solution and configure MySQL.
- Implement EF Core entities, migrations, and relationships.
- Add ASP.NET Core Identity, login, and logout.
- Create roles, user management, and the admin dashboard.
- Configure school settings, academic years, classes, and sections.

Deliverable: an authenticated application with working database CRUD.

Phase 2 — Admissions & Students

- Admission forms and document uploads.
- Review and approval workflow.
- Student and guardian management.
- Class enrollment and unique student IDs.
- Student search, filters, pagination, and exports.

Deliverable: complete admission-to-enrollment workflow.

Phase 3 — Teachers & Attendance

- Teacher profiles and subject assignments.
- Attendance entry and correction approvals.
- Daily and monthly reports.
- Parent access to attendance records.

Deliverable: working teacher dashboard and attendance register.

Phase 4 — Examinations & Results

- Examination and subject setup.
- Marks entry and validation.
- Result submission and approval.
- Grade calculation and report card generation.
- Revision history.

Deliverable: complete examination-to-publication workflow.

Phase 5 — Fees & Finance

- Fee structures and student fee assignments.
- Invoice generation and discount approvals.
- Partial payments and outstanding balances.
- Payment gateway sandbox integration.
- Receipts, overdue reminders, and financial reports.

Deliverable: reliable end-to-end fee collection.

Phase 6 — Imports, Exports & Communication

- Excel import/export and bulk validation.
- PDF invoices, receipts, and report cards.
- ZIP downloads and document access controls.
- SMTP email, announcements, and parent-teacher messaging.
- Audit logs and background jobs.

Deliverable: the full set of common web development operations.

Phase 7 — Testing & Deployment

- Unit tests for calculations and validation.
- Integration tests for database operations and authorization.
- End-to-end tests for admissions, results, and payments.
- Security checks, backup strategy, and production configuration.
- Deploy to a Linux server or suitable cloud platform.

Deliverable: a tested, deployable application.

## 10. Testing checklist

Prioritize these cases before calling the project complete:

- An unauthorized user cannot access another student's private records.
- A teacher cannot edit marks for a subject they do not teach.
- An application cannot be approved twice to create duplicate enrollment.
- A student cannot be enrolled into a full section.
- Marks cannot exceed the configured full marks.
- A published result cannot be changed without an authorized revision.
- A payment callback cannot mark an unpaid transaction as successful without verification.
- Duplicate payment callbacks do not create duplicate allocations.
- Discounts cannot make an invoice negative unless the business rules explicitly permit credits.
- Failed Excel imports do not leave partially corrupted data.
- Audit logs record sensitive actions and cannot be edited by ordinary users.

## 11. Scope control

Do not build every module at once. The first usable version should contain:

1. Authentication and RBAC.
2. Academic setup, students, teachers, and guardians.
3. Admission and enrollment approvals.
4. Fee invoices, manual payment recording, and receipts.
5. Basic examinations and result approvals.
6. Audit logs and Excel/PDF operations.

Add the real payment gateway, messaging, automated reminders, and more advanced reporting after the core workflows are reliable.

Since your goal is to learn .NET alongside agent-assisted development, implement one vertical feature at a time: database model → migration → business service → Blazor UI → tests. Make sure you can explain the relevant C#, dependency injection, EF Core, authorization, and Blazor concepts before moving on.&#x20;