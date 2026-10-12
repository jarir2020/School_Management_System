# Phase 2 — Admissions and students

Phase 2 follows an application from public submission through staff review and enrollment. It also adds student and guardian records, enrollment history, paginated search, and CSV export. Fee invoices and payments remain in Phase 5; student transfers and yearly promotion are later work.

## 1. The admission data model

- **Admission application:** stores the submitted student and guardian details, selected academic year, optional preferred section, a generated application number, and its review status.
- **Application document:** stores the document type, original display name, detected content type, size, and a random storage key. The file bytes live outside the web root.
- **Admission review:** records each decision, the reviewer account, remarks, and timestamp. Rejected and approved applications remain in the queue for history.
- **Student and guardian:** approval creates one student, one primary guardian, and a `StudentGuardian` link. The application ID and student number are unique.
- **Enrollment:** connects the student, academic year, and section. A unique `(StudentId, AcademicYearId)` index prevents duplicate enrollment for the same school year.
- **Student status history:** records initial admission and later status changes with the acting staff account and a reason.

These relationships are configured in `ApplicationDbContext`. The `InitialPhase2` EF Core migration creates the new tables and constraints without replacing Phase 1 data.

## 2. Public application and private uploads

Visitors use `/admissions/apply`; no account is needed to submit. They choose an academic year, can request a section, and provide student and guardian details. Supporting files are optional. The form accepts a student photo, birth certificate, and one other document.

The server limits each file to 5 MB and checks the file signature for PDF, PNG, or JPEG data. A photo must be a PNG or JPEG image. The browser's file extension and content type are not trusted. `LocalPrivateDocumentStore` saves bytes outside `wwwroot` using a random storage key; the database keeps the sanitized display name and metadata.

Only Super Admins and Admission Officers can open the review pages and download files. The file endpoint checks the same roles on the server, so knowing a document URL does not grant access.

## 3. Review and enrollment

Admission Officers and Super Admins use `/admin/admissions` to search and page through applications. The detail page shows the student, guardian, requested year/section, review history, and links to private documents.

Rejecting requires remarks and records the reviewer, decision, time, and explanation. Approval requires choosing a section with capacity. One serializable database transaction then:

1. Rechecks the application is still pending and the section has a place.
2. Creates a unique year-prefixed student number.
3. Creates the student and guardian link.
4. Creates the academic-year enrollment and initial status-history row.
5. Marks the application approved and writes the review record.

The transaction prevents a partial student record if one step fails. Unique database constraints protect student numbers, application numbers, and duplicate enrollments. A completed application cannot be approved or rejected a second time.

## 4. Student directory

`/students` is available to Super Admins and Admission Officers. It searches student names, student numbers, and guardian phone numbers; filters by status, academic year, and section; and shows 20 rows per page. The CSV action exports all rows matching the current filters. CSV fields are quoted, and formula-leading text is prefixed so spreadsheet applications treat it as data.

Open a student record to edit student and primary guardian details, see enrollment and status history, or withdraw an active student. A withdrawal requires a reason, closes active enrollments, and appends a status-history record. Student records are not deleted, preserving those histories.

## 5. Database and file setup

The app uses the same `ConnectionStrings:SchoolManagement` User Secret as Phase 1. In Development, application startup applies both checked-in migrations. Private uploads default to `.local/private-documents` at the repository root; `.local/` is ignored by Git. Set `Storage:PrivateDocumentRoot` in local configuration if documents need to live in a different private directory.

To generate a future migration after editing entities or EF mappings:

```sh
dotnet tool restore
dotnet tool run dotnet-ef migrations add DescribeYourChange --project src/SchoolManagement.Infrastructure --startup-project src/SchoolManagement.Web --output-dir Persistence/Migrations
```

## 6. Manual end-to-end walkthrough

Use fictional student and guardian data:

1. Sign in as Super Admin and create an academic year, a class, and a section with a small capacity.
2. Open `/admissions/apply` in a signed-out browser and submit an application. Attach sample PDF/JPEG files if available.
3. Sign in as an Admission Officer or Super Admin and open `/admin/admissions`.
4. Review the application, open its files, then approve it into the requested section.
5. Confirm the new student appears at `/students` with a generated student number, guardian phone, and enrollment.
6. Update the student's guardian details and export the filtered directory to CSV.
7. Withdraw the student with a reason and confirm the status history records the change.
8. Submit a second application and reject it with remarks. Confirm no student or enrollment is created for the rejected application.

The solution build passed and the migration was applied to the local database. The public admissions route returned HTTP 200. The complete submission, review, file-download, approval, and CSV workflow still needs this manual walkthrough.
