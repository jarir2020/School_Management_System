# School_Management_System

A learning-focused school administration and fee management platform built with ASP.NET Core, Blazor, and MySQL.

## Project status

Phases 1–7 are implemented in source. Phase 7 adds grade-calculation unit tests, SQLite-backed workflow tests for admissions, teacher marks, and verified/underpaid payments, a NuGet vulnerability audit, MySQL migration and production-startup checks in CI, production health endpoints, container configuration, an explicit migration command, and a private MySQL backup script. All 10 local unit and workflow tests passed; no vulnerable packages were reported. All migrations also applied to a disposable MySQL 8.4 database, and the Production health smoke check returned `ready` and `live`. All six migrations have since been applied to the local `school_management` database; its migration history and 49 tables were verified. The local database also passed a Production health smoke check, and a backup restored successfully into a disposable database. The Phase 2–7 browser workflows still need manual walkthroughs. SSLCOMMERZ checkout/refunds need merchant credentials and a public HTTPS callback host. SMTP delivery needs local SMTP configuration. No production deployment has been performed.

## Technology

- C# and ASP.NET Core on .NET 9
- Blazor Web App with Interactive Server rendering
- MySQL 8.4 with Entity Framework Core 9 and Pomelo 9
- ASP.NET Core Identity with role and policy-based authorization
- ClosedXML for Excel, QuestPDF for PDFs, and SMTP-backed background email
- xUnit v3 unit and service-integration tests, with a MySQL migration/startup smoke check in GitHub Actions

The application is a modular monolith. Admissions, academics, fees, and communication are separate modules within the same application.

## Implemented modules

- School setup, academic years, classes, sections, subjects, and teacher assignments
- Admissions, student and guardian records, and enrollment approvals
- Teacher assignments, attendance, examinations, and approved results
- Fee structures, invoices, discounts, payments, receipts, and reports
- Private document uploads and ZIP downloads, announcements, parent-teacher messaging, audit logs, and background email

## Roadmap

The project plan defines seven phases: foundation; admissions and students; teachers and attendance; examinations and results; fees and finance; imports, exports, and communication; then testing and deployment. Each phase should reach a working, reviewable state before the next begins.

## Local setup

Install the .NET 9 SDK and MySQL 8.4. Create a local MySQL database and a database user for the application. Store the connection string and bootstrap administrator details with .NET User Secrets:

```sh
dotnet user-secrets set "ConnectionStrings:SchoolManagement" "Server=localhost;Port=3307;Database=school_management;User ID=school_app;Password=<local-secret>" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:Password" "<choose-a-strong-secret>" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:FullName" "School Administrator" --project src/SchoolManagement.Web
```

Replace the password placeholders locally. They are examples only; never commit real connection strings or credentials.

Restore, compile, run the tests, and start the app from the repository root:

```sh
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln
dotnet test SchoolManagement.sln
dotnet run --project src/SchoolManagement.Web
```

In Development, the app applies checked-in EF Core migrations and seeds the six roles. It creates the first Super Admin only when SeedAdmin:Email and SeedAdmin:Password are configured. Public registration is disabled; later accounts are created from User management. Anonymous visitors can submit admission applications; only Super Admins and Admission Officers can review them and access private documents. Configure SSLCOMMERZ sandbox values with User Secrets as described in the Phase 5 guide in guides/PHASE5_FEES_FINANCE_QUICK_GUIDE.md. Configure SMTP values with User Secrets as described in the Phase 6 guide in guides/PHASE6_IMPORTS_EXPORTS_COMMUNICATION_QUICK_GUIDE.md. See the phase guides in the guides directory.

## Production preparation

The app does not apply migrations automatically outside Development. Review [the Phase 7 testing and deployment guide](guides/PHASE7_TESTING_DEPLOYMENT_QUICK_GUIDE.md) before deploying. It covers required production settings, the Linux Docker Compose setup, migration order, health endpoints, backups, restore drills, and manual browser checks. Start with `.env.example`; keep `.env`, MySQL client option files, private documents, Data Protection keys, and backups out of Git. The Compose example binds the app and MySQL ports to loopback for a separately managed TLS reverse proxy.

The current .NET 9 and EF Core 9 target reaches end of support on November 10, 2026. The production guide records the provider compatibility gate and requires a supported framework/provider upgrade before a production release. See the [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

Follow [`AGENTS.md`](AGENTS.md) for implementation guidance. The current .NET 9 / EF Core 9 / Pomelo 9 combination was selected to match the installed SDK and provider compatibility. Upgrade the target framework after a stable compatible Pomelo release is available.

## Project documents

- [`plan.md`](plan.md) — architecture, data model, workflows, and implementation roadmap
- [`AGENTS.md`](AGENTS.md) — repository workflow and coding guidance
- [`llms.txt`](llms.txt) — short index for AI and documentation crawlers
- [`llms-full.txt`](llms-full.txt) — expanded project context for AI tools
- [`LICENSE`](LICENSE) — MIT License
- [`guides/PHASE1_THEORY_QUICK_GUIDE.md`](guides/PHASE1_THEORY_QUICK_GUIDE.md) — foundation concepts and setup walkthrough
- [`guides/PHASE2_ADMISSIONS_QUICK_GUIDE.md`](guides/PHASE2_ADMISSIONS_QUICK_GUIDE.md) — admissions workflow, private files, review, enrollment, and student directory
- [`guides/PHASE3_TEACHERS_ATTENDANCE_QUICK_GUIDE.md`](guides/PHASE3_TEACHERS_ATTENDANCE_QUICK_GUIDE.md) — teacher setup, attendance, corrections, reports, and parent access
- [`guides/PHASE4_EXAMINATIONS_RESULTS_QUICK_GUIDE.md`](guides/PHASE4_EXAMINATIONS_RESULTS_QUICK_GUIDE.md) — examination setup, marks, approval, report cards, and revisions
- [PHASE5_FEES_FINANCE_QUICK_GUIDE.md](guides/PHASE5_FEES_FINANCE_QUICK_GUIDE.md) — fee assignments, discount approvals, collections, receipts, sandbox checkout, refunds, and overdue reminders
- [PHASE6_IMPORTS_EXPORTS_COMMUNICATION_QUICK_GUIDE.md](guides/PHASE6_IMPORTS_EXPORTS_COMMUNICATION_QUICK_GUIDE.md) — spreadsheets, protected downloads, announcements, messaging, SMTP, audit logs, and background jobs
- [PHASE7_TESTING_DEPLOYMENT_QUICK_GUIDE.md](guides/PHASE7_TESTING_DEPLOYMENT_QUICK_GUIDE.md) — tests, CI, production settings, Linux containers, backups, restore drills, and deployment checks
