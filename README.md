# School_Management_System

A learning-focused school administration and fee management platform planned with ASP.NET Core, Blazor, and MySQL.

## Project status

This repository currently contains the project plan and contributor guidance. Application source has not been created yet. The implementation roadmap is in [`plan.md`](plan.md).

## Planned technology

- C# and ASP.NET Core
- Blazor Web App with Interactive Server rendering
- MySQL with Entity Framework Core and Pomelo
- ASP.NET Core Identity with role and policy-based authorization
- xUnit and integration tests as the application grows

The application will start as a modular monolith. Admissions, academics, fees, and communication will be separate modules within the same application.

## Planned capabilities

- School setup, academic years, terms, classes, sections, and subjects
- Admissions, student and guardian records, and enrollment approvals
- Teacher assignments, attendance, examinations, and approved results
- Fee structures, invoices, discounts, payments, receipts, and reports
- Private document uploads, Excel and PDF operations, notifications, and audit logs

## Roadmap

The project plan defines seven phases: foundation; admissions and students; teachers and attendance; examinations and results; fees and finance; imports, exports, and communication; then testing and deployment. Each phase should reach a working, reviewable state before the next begins.

## Development

The solution has not been scaffolded yet, so there are no build or run commands at this stage. Follow [`AGENTS.md`](AGENTS.md) when adding implementation. Never commit real connection strings, credentials, or private student data.

## Project documents

- [`plan.md`](plan.md) — architecture, data model, workflows, and implementation roadmap
- [`AGENTS.md`](AGENTS.md) — repository workflow and coding guidance
- [`llms.txt`](llms.txt) — short index for AI and documentation crawlers
- [`llms-full.txt`](llms-full.txt) — expanded project context for AI tools
- [`LICENSE`](LICENSE) — MIT License
