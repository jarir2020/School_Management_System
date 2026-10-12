# Phase 3 — Teachers and Attendance

This walkthrough explains how subject assignments, daily registers, corrections, reports, and parent access work in the school management app.

## What Phase 3 adds

- Subjects and teacher assignments by class section and academic year.
- A teacher dashboard that lists only the signed-in teacher's assignments.
- One attendance register per section, subject, academic year, and date.
- Present, absent, late, and excused statuses with an optional note.
- Correction requests that a Principal or Super Admin must review.
- Daily register summaries and monthly student summaries.
- Parent access through a staff-managed link from a guardian record to a Parent/Student account.

Teacher names and contact details reuse the existing Identity account and `UserProfile`; no second teacher identity is created.

## Roles and access rules

| Role | Phase 3 access |
| --- | --- |
| Super Admin | Create subjects and assignments; link parent accounts; enter attendance only if also assigned as a Teacher; view all reports and review corrections |
| Teacher | See assigned sections and subjects, submit each register once, request corrections for their own records, view only their own report data |
| Principal | View school-wide reports and approve or reject correction requests |
| Parent/Student | View attendance for children connected to a guardian account link managed by the Super Admin |

The service checks roles and record ownership on the server. The UI's role attributes are an additional boundary, not the only authorization check.

## Staff setup

1. Sign in as Super Admin.
2. In **User management**, create a staff account with the Teacher role. Create Parent/Student accounts for guardians who need portal access.
3. In **Teachers and subjects**, add each subject with a unique short code, then assign a teacher, class section, and academic year.
4. Open the student record from **Students**. In **Parent portal access**, link a Parent/Student account to the matching guardian record.

Assignments can be removed before attendance exists. Once a register has been saved for an assignment, the app preserves the assignment so its attendance history remains attributable.

## Taking a register

1. Sign in with a Teacher account and open **Teacher dashboard**.
2. Choose **Take today's attendance** for an assigned subject. The date must fall within that assignment's academic year.
3. Select a status for each active student enrolled in the section for that year. A note is optional.
4. Submit once. A database unique key prevents a second register for the same section, subject, academic year, and date; a second key prevents duplicate student rows inside a register.

After submission the register is read-only. If a student or teacher spots a mistake, the teacher submits a correction request with a reason. A different Principal or Super Admin may approve or reject it. Approval changes the current record only when it still matches the status the teacher originally saw, and the request keeps the before/after values and review details.

## Reports and parent view

- **Daily report** groups the registers submitted for one date and counts each status.
- **Monthly report** groups saved records by student, section, and subject. The displayed attendance percentage treats Present, Late, and Excused as attended statuses; the school can revisit that policy if it needs a different convention.
- **Children's attendance** filters by the signed-in parent account's guardian links. A parent cannot supply another student's ID to widen the query. A date range is limited to one year.

If a parent cannot see a child, a Super Admin should check that the right account is linked to a guardian who is connected to that student's record. A link does not expose admission documents or unrelated student records.

## Database and local run

The `Phase3TeachersAndAttendance` EF Core migration creates subjects, assignments, sessions, records, correction requests, and guardian account links. Development startup applies checked-in migrations. Production environments should apply migrations as an explicit deployment step.

Use the repository's local setup instructions in [`README.md`](../README.md), then run the app in Development and sign in with the local Super Admin credentials stored in User Secrets. Do not put account credentials, student data, or database connection strings in this guide or source control.

## Verification status

The solution compiled with zero warnings and zero errors. The Phase 3 migration was applied to the configured local MySQL database. The full teacher-to-approval-to-parent flow still needs a manual browser walkthrough with sample accounts and student records.
