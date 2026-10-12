# School_Management_System

A learning-focused school administration and fee management platform built with ASP.NET Core, Blazor, and MySQL.

## Project status

Phase 1 implementation is in place: the four-project solution, MySQL-backed Identity, school setup CRUD, Super Admin account management, and the initial migration are ready. The solution builds successfully. Running the app and exercising its CRUD still requires a reachable MySQL 8.4 server. The full scope and roadmap are in [`plan.md`](plan.md).

## Technology

- C# and ASP.NET Core on .NET 9
- Blazor Web App with Interactive Server rendering
- MySQL 8.4 with Entity Framework Core 9 and Pomelo 9
- ASP.NET Core Identity with role and policy-based authorization
- xUnit and integration tests as the application grows

The application is a modular monolith. Admissions, academics, fees, and communication will be separate modules within the same application.

## Planned capabilities

- School setup, academic years, terms, classes, sections, and subjects
- Admissions, student and guardian records, and enrollment approvals
- Teacher assignments, attendance, examinations, and approved results
- Fee structures, invoices, discounts, payments, receipts, and reports
- Private document uploads, Excel and PDF operations, notifications, and audit logs

## Roadmap

The project plan defines seven phases: foundation; admissions and students; teachers and attendance; examinations and results; fees and finance; imports, exports, and communication; then testing and deployment. Each phase should reach a working, reviewable state before the next begins.

## Local setup

Install the .NET 9 SDK and MySQL 8.4. Create a local MySQL database and a database user for the application. Store the connection string and bootstrap administrator details with .NET User Secrets:

```sh
dotnet user-secrets set "ConnectionStrings:SchoolManagement" "Server=localhost;Port=3306;Database=school_management;User ID=school_app;Password=<local-secret>" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:Password" "<choose-a-strong-secret>" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:FullName" "School Administrator" --project src/SchoolManagement.Web
```

Replace the password placeholders locally. They are examples only; never commit real connection strings or credentials.

Restore, compile, and run from the repository root:

```sh
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln
dotnet run --project src/SchoolManagement.Web
```

In Development, the app applies checked-in EF Core migrations and seeds the six roles. It creates the first Super Admin only when `SeedAdmin:Email` and `SeedAdmin:Password` are configured. Public registration is disabled; later accounts are created from User management. Phase 1 setup, sign-in, and admin page details are in [`guides/PHASE1_THEORY_QUICK_GUIDE.md`](guides/PHASE1_THEORY_QUICK_GUIDE.md).

Follow [`AGENTS.md`](AGENTS.md) for implementation guidance. The current .NET 9 / EF Core 9 / Pomelo 9 combination was selected to match the installed SDK and provider compatibility. Upgrade the target framework after a stable compatible Pomelo release is available.

## Project documents

- [`plan.md`](plan.md) — architecture, data model, workflows, and implementation roadmap
- [`AGENTS.md`](AGENTS.md) — repository workflow and coding guidance
- [`llms.txt`](llms.txt) — short index for AI and documentation crawlers
- [`llms-full.txt`](llms-full.txt) — expanded project context for AI tools
- [`LICENSE`](LICENSE) — MIT License
- [`guides/PHASE1_THEORY_QUICK_GUIDE.md`](guides/PHASE1_THEORY_QUICK_GUIDE.md) — foundation concepts and setup walkthrough
