# Phase 4 — Examinations and Results

This guide walks through examination setup, teacher mark entry, review, publication, parent report cards, and corrections to published marks.

## What Phase 4 adds

- Academic terms and examinations tied to an academic year.
- Section and subject exam registers with configured full and pass marks.
- Teacher mark entry limited to the teacher's assigned section, subject, and academic year.
- Complete-roster submission, Principal/Super Admin review, rejection and resubmission, and publication.
- Academic-year grade bands and on-screen report cards for linked parent accounts.
- Reason-required Super Admin revisions with before/after history for published marks.

The report card is an on-screen view. PDF report card export is in Phase 6.

## Roles and access rules

| Role | Phase 4 access |
| --- | --- |
| Super Admin | Configure terms, examinations, subjects, and grade bands; open exams; revise published marks; review, approve, and publish results |
| Teacher | Save and submit marks only for an open subject assigned to them in the examination's academic year |
| Principal | Review, approve, reject, and publish submitted results; cannot review an examination whose marks they entered or updated |
| Parent/Student | View published report cards only for students linked to the signed-in account through guardian records |

Each service operation checks the user's role and record scope on the server. Page role attributes provide an additional UI boundary.

## Set up the grading rules and examination

1. Confirm the academic year, class sections, students, subjects, and teacher assignments exist.
2. Sign in as Super Admin and open **Examinations**.
3. Add one or more terms. Each term's dates must fall inside its academic year.
4. Add grade bands for each academic year. Use distinct percentage thresholds from 0 to 100; a 0% band is required before an examination can open. Each band has a letter grade, grade point, pass flag, and remark.
5. Create an examination under the matching academic year and term.
6. Add each section and subject that will receive marks, with its full marks and pass marks. A teacher must already be assigned to that subject, section, and academic year.
7. Finish the complete grading scale before opening any exam in the academic year. Once an exam is opened, the grade bands are locked so published grades cannot drift when someone adds a new threshold.
8. Open the examination. After opening, the subject list and mark limits are fixed for this workflow.

The system selects the grade band with the greatest minimum percentage that does not exceed the subject percentage. Grade-band setup currently adds bands; plan the thresholds before opening an examination.

## Enter and submit marks

1. Sign in as an assigned Teacher and open **Marks entry**.
2. Select an open section/subject examination register. The register lists active students currently enrolled in that section and academic year.
3. Save marks as a draft. Draft rows may be blank, but each submitted value must be from zero through the configured full marks. The submitted roster must still match the current active roster.
4. Fill every student's mark and choose **Submit complete subject**. The page saves the latest values first, then submits the subject. The service rejects empty marks or a roster that changed since the register was loaded.
5. Repeat for every configured subject. The examination enters the review queue only after all subjects are submitted.

After submission, that subject is locked. If a reviewer rejects the examination, every subject register reopens for corrections and the complete examination must be submitted again.

## Review, approve, and publish

1. A Principal or Super Admin opens **Review results** after all subjects are submitted.
2. Review the examination summary and prior decisions. A user who entered or last updated marks in the examination cannot review it.
3. Approve the results or reject them with a reason. Both decisions are saved with the reviewer and timestamp. Rejection returns all registers to mark entry.
4. After approval, publish the examination when report cards are ready to release.

Only published examinations appear in the parent portal. A Parent/Student account sees cards only for students whose guardian records are linked to that account by a Super Admin.

## Report-card calculations

- A subject percentage is `marks obtained / full marks × 100`, rounded to two decimal places.
- Its letter grade and grade points come from the matching configured band. The subject passes only when that band is marked passing **and** the mark reaches the subject's pass mark.
- Total marks and full marks are summed across the student's configured examination subjects. Overall percentage is the weighted total `total marks / total full marks × 100`, rounded to two decimals.
- GPA is the simple average of subject grade points, rounded to two decimals. Overall pass requires every subject to pass.

## Revise a published mark

Published marks cannot be changed through ordinary teacher entry. A Super Admin opens **Published result revisions**, selects an examination, enters the corrected mark and a reason, and saves the revision. The service checks the new mark against the original full marks and records the previous mark, new mark, reason, account, and time. The report card then reflects the revised value, while the revision history remains available on the same screen.

## Database and local run

The `Phase4ExaminationsResults` EF Core migration creates terms, examinations, exam subjects, student marks, approval decisions, revision history, and academic-year grade bands. The migration is applied to the configured local MySQL database. Development startup applies checked-in migrations; production environments should apply migrations as an explicit deployment step.

Use the local setup instructions in [`README.md`](../README.md), start the app in Development, and use the local Super Admin and staff accounts. Keep database credentials, account passwords, and real student information in local configuration or the database, never in this guide or source control.

## Verification status

The solution built with zero warnings and zero errors, EF reports no model changes beyond the checked-in migration, and the Phase 4 migration was applied to the configured local MySQL database. The full setup-to-parent report-card workflow still needs a manual browser walkthrough with sample accounts, assignments, enrollments, and grade bands.
