# Agent and contributor instructions

## Start with the project plan

- Read `plan.md` before implementation. Treat it as the source for project scope, architecture, workflows, and phase order.
- Build one roadmap phase at a time. Do not implement later modules as a shortcut around an unfinished earlier phase.
- Work on one vertical feature at a time: data model and migration, business service, Blazor UI, then the relevant verification and guide updates.

## Keep the code teachable

- Use clear names and straightforward C# before adding abstractions.
- Add concise comments where they explain a non-obvious decision or a framework boundary. Explain the broader concept in a phase guide rather than narrating every line.
- Keep business calculations and rules in services, not Blazor components or persistence entities.
- Use `decimal` for money. Keep school timezone and due-date behavior explicit.
- Prefer the modular monolith described in `plan.md`; do not introduce microservices or generic layers without a concrete need.

## Protect data and permissions

- Enforce authorization on the server for every protected operation and record-level access.
- Never commit credentials, real connection strings, private student data, uploaded documents, or local database files.
- Store admission documents outside the public web root and authorize downloads on the server.
- Use database constraints and transactions for uniqueness and multi-step workflows.
- Preserve approval and audit history. Do not silently overwrite published results or verified payments.

## Keep project documentation current

- Update `README.md` when setup, run, or phase status changes.
- Add or update a focused guide for each completed learning phase.
- Keep `llms.txt` and `llms-full.txt` aligned with the repository's actual state; do not describe planned features as implemented.
- Run the Phase 7 unit and service-integration tests with `dotnet test SchoolManagement.sln`. The GitHub Actions workflow also applies migrations to a disposable MySQL 8.4 service and starts the app in Production to check its health endpoints.
- Production must receive an explicit database connection, trusted reverse-proxy IP, allowed host names, persistent Data Protection key path, and absolute private-document path. Do not enable automatic production migrations.
- Backups contain private student and financial data. Keep MySQL client credentials in a protected file, store backup copies outside the application host, and verify restoration in an isolated database.

## Worktree care

- Inspect Git status before editing and preserve unrelated changes.
- Stage only files that belong to the requested change.
- Keep generated build output, local settings, secrets, and database files out of commits.
