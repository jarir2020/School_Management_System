# ASP.NET Core and Blazor: Theory Notes for Interviews

This guide explains the ideas used by this project, from basic .NET vocabulary to common interview topics. It connects ASP.NET Core to Spring Boot and Laravel, and Blazor to React, as starting points only; the frameworks do not map one-to-one.

## The project in one picture

```text
Browser
  └── ASP.NET Core host
        ├── Blazor Web App (Interactive Server components)
        ├── Identity sign-in and account components
        ├── authorized HTTP endpoints for files, reports, and payment callbacks
        └── application services
              ├── EF Core DbContext factory
              │     └── Pomelo MySQL provider → MySQL 8.4
              ├── private document storage
              ├── SSLCOMMERZ gateway client
              └── hosted email worker → configured SMTP server
```

For an admission review, the usual path is:

```text
ReviewQueue.razor → IAdmissionService → AdmissionService
→ IApplicationDbContextFactory → ApplicationDbContext / EF Core
→ Pomelo provider → MySQL
→ admission summaries → Blazor renders the updated page
```

The browser and application use one web host. Blazor handles the interactive staff and parent pages on the server. A small set of HTTP endpoints in `Program.cs` returns files, receives payment callbacks, and reports application health; this is not a separate frontend talking to a standalone JSON API.

## Quick framework comparison

These are useful parallels, not exact replacements.

| Spring Boot or Java idea | ASP.NET Core / .NET idea in this project |
| --- | --- |
| Java compiler, JVM, bytecode | C# compiler, .NET assemblies and Intermediate Language, .NET runtime |
| Maven and `pom.xml` | .NET SDK, NuGet packages, and `.csproj` files |
| Spring Boot application startup | `WebApplication.CreateBuilder` and `app.Run()` in `Program.cs` |
| Spring `ApplicationContext` and beans | ASP.NET Core dependency injection container and registered services |
| Servlet filters and Spring Security chain | ASP.NET Core middleware pipeline and authentication/authorization services |
| Spring MVC controller | Blazor route components and Minimal API endpoint handlers; separate mechanisms |
| JPA / Hibernate | Entity Framework Core; Pomelo connects EF Core to MySQL |
| Flyway migrations | EF Core migrations |
| Spring Security users and authorities | ASP.NET Core Identity users, roles, claims, and authorization policies |
| AngularJS or React views | Blazor Razor components; C# event handlers update server-rendered UI |
| Spring `@Service` annotation | A C# service class registered in the DI container; there is no required service annotation |
| Spring `@Transactional` | EF Core transaction APIs, usually coordinated by a service workflow |
| Spring `@Scheduled` / executor | .NET `IHostedService` or `BackgroundService`; this app uses a hosted email worker |
| JUnit or Spring tests | xUnit tests and ASP.NET Core / database integration tests |

## Part I — C# and .NET foundations

### Level 1: Language and runtime

### 1. C#, .NET, and ASP.NET Core

**C#** is the programming language. The **.NET SDK** compiles and builds projects, restores dependencies, and runs development tools. The **.NET runtime** executes the compiled application. **ASP.NET Core** is the web framework used to build the HTTP host and web application.

This repository targets `net9.0`. Its web project uses the ASP.NET Core Web SDK, while the other projects use the regular .NET SDK. The project target is declared in each `.csproj` file.

### 2. Solution, project, and NuGet

A `.sln` solution groups related projects so the CLI and IDE can build them together. A `.csproj` file declares a project's target framework, package dependencies, and references to other projects. NuGet restores package dependencies.

From the repository root, common commands are:

```sh
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln
dotnet run --project src/SchoolManagement.Web
```

`dotnet build` compiles the projects and Razor components. `dotnet run` starts the selected web project. Tests are a separate command; a successful build alone does not prove that business workflows behave correctly.

### 3. Assembly, namespace, class, and interface

- An **assembly** is a compiled `.dll` or executable output from a project.
- A **namespace** groups types and avoids naming collisions. It does not have to match a folder, although this project keeps them aligned.
- A **class** defines data and behavior; an **object** is an instance of a class.
- An **interface** describes a contract that one or more classes implement.
- An **enum** represents a fixed set of named values, such as `AdmissionStatus` or `PaymentStatus`.
- An **attribute**, written in square brackets, attaches metadata that a framework can inspect. For example, `[Authorize]` declares an access requirement on a Razor component.

### 4. Nullable reference types and validation

Nullable reference types are enabled in the projects. `string` means the code expects a non-null value; `string?` means `null` is an allowed state. This is a compiler aid, not a substitute for validating untrusted form or file input.

The project uses validation in several places: Blazor form validation, explicit service checks, data annotations, and database constraints. Each serves a different purpose. Client-side messages help the user; server-side checks protect the application; database constraints protect stored data against duplicate or invalid relationships.

### 5. Records, DTOs, and entities

A **record** is useful for data that is passed between parts of the application, such as search filters, form input, and result summaries. A **DTO** (Data Transfer Object) describes data crossing a boundary. An **entity** represents data with identity and persistence relationships, such as `Student` or `FeeInvoice`.

These types should not be treated as interchangeable. Returning a compact summary model to a page avoids loading or exposing every field and relationship of a database entity.

### 6. `async`, `Task`, and cancellation

Database, file, and network operations are I/O-bound. `async` and `await` let the server release a thread while waiting for those operations to finish. An asynchronous method commonly returns `Task` or `Task<T>`.

```csharp
await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
var years = await db.AcademicYears
    .AsNoTracking()
    .OrderByDescending(year => year.StartDate)
    .ToListAsync(cancellationToken);
```

The `CancellationToken` lets a request or background operation stop work when its caller no longer needs the result. Do not use `.Result` or `.Wait()` to block on asynchronous I/O in ASP.NET Core; blocking can waste threads and contribute to thread-pool starvation.

## Part II — Application structure and ASP.NET Core

### Level 2: Projects and dependency injection

### 7. The four project areas

| Project | Main responsibility | Examples |
| --- | --- | --- |
| `SchoolManagement.Core` | School domain entities, enums, and role names without web-host code | `Student`, `FeeInvoice`, `AdmissionStatus`, `SchoolRoles` |
| `SchoolManagement.Application` | Use-case contracts, input/output models, and business workflows | `IAdmissionService`, `AdmissionSearchFilter`, `GradeCalculator` |
| `SchoolManagement.Infrastructure` | Database, Identity stores, MySQL provider, private storage, gateway, and background worker | `ApplicationDbContext`, EF migrations, `SslCommerzGateway` |
| `SchoolManagement.Web` | ASP.NET Core host, Blazor pages, account UI, routing, and HTTP endpoints | `Program.cs`, `ReviewQueue.razor`, `/health/ready` |

The intended references point toward the domain: the web project composes the application and infrastructure, infrastructure implements persistence and external-system details, and the core remains independent of the web host. This is the repository's modular-monolith layout, not a folder structure required by .NET. The application project also uses EF Core abstractions for its database interface, so the boundary is practical rather than a claim that it has no persistence dependency at all.

### 8. Inversion of Control and dependency injection

**Inversion of Control (IoC)** means application code does not create and manage every dependency itself. **Dependency injection (DI)** is the mechanism that provides those dependencies. ASP.NET Core builds a service provider from registrations in `Program.cs` and `AddSchoolManagementInfrastructure`.

For example, a page can request an interface rather than constructing an EF context:

```razor
@inject IAdmissionService Admissions
```

The application registers a concrete implementation for that interface. This reduces coupling and makes the service easier to replace in tests.

Common service lifetimes are:

- **Transient:** a new instance is created each time the service is requested.
- **Scoped:** one instance per web request; in an Interactive Server Blazor circuit, scoped services can live for the circuit.
- **Singleton:** one shared instance for the application process; it must be safe to use concurrently.

The application uses `IDbContextFactory<ApplicationDbContext>` to create short-lived contexts per operation. A `DbContext` tracks changes and is not thread-safe, so a single context should not be shared across unrelated UI events or concurrent work.

### 9. The host and `Program.cs`

`WebApplication.CreateBuilder(args)` creates the host builder. It loads configuration, logging, and environment details, and exposes the service collection. The app registers Blazor, authentication, authorization, infrastructure, and other services before calling `builder.Build()`.

After building, `Program.cs` configures middleware and maps endpoints and Razor components. Finally, `app.Run()` starts the web server. This is the composition root: the place that connects implementations and configures the whole application.

### Level 3: Requests, pages, and components

### 10. Middleware and the HTTP pipeline

Middleware is a sequence of components that can inspect or change an HTTP request and response. The order matters because a component can pass control to the next one or finish the response itself.

This project configures forwarded headers, error handling and HSTS in production, HTTPS redirection, authentication, authorization, and antiforgery before mapping the application. Authentication establishes the current user; authorization checks whether that user may continue to a protected endpoint or component.

Middleware is for broad request concerns. Admission, attendance, or fee business rules belong in the relevant service, where they can also apply to calls that do not come through one particular page.

### 11. Blazor Web App and Interactive Server rendering

Blazor **Razor components** combine markup, C# state, and event handlers. This project uses the Interactive Server render mode: the server renders the UI and keeps an interactive connection (a Blazor circuit) with the browser. When the user clicks a button, the event is sent to the server, the C# handler runs, and an updated render is sent back.

This differs from a React single-page application, where the main application code runs in the browser and usually calls a separate API. Blazor can also use WebAssembly, but this application uses Interactive Server. It therefore needs a reliable connection while an interactive page is open, and server-side authorization remains essential even when the UI hides a link.

The `Router` in `Routes.razor` matches a URL to a component with an `@page` directive. Components can inject services, bind form fields, and handle events such as `@onclick`. `App.razor` selects the render mode for the routed UI.

### 12. Page, service, and database responsibilities

- **Razor component:** displays data, collects user input, and handles UI events.
- **Application service:** validates a use case, applies business rules, checks record access, and coordinates persistence or external services.
- **Core entity:** represents a school concept and its stored state.
- **Infrastructure:** connects the use case to MySQL, Identity, file storage, email, or a payment provider.

Keeping the page focused on presentation means the same business rule can be called by a page, endpoint, background operation, or test without copying it into each UI handler.

### 13. HTTP endpoints alongside Blazor

Most staff workflows use routed Razor components. `Program.cs` also maps Minimal API endpoints for tasks that naturally return an HTTP response, including CSV/XLSX/PDF/ZIP downloads, payment provider callbacks, and health checks.

An endpoint handler can bind route values, query values, services, and a `CancellationToken` from the request. It then returns a result such as a file, redirect, status code, or JSON object. A mapped endpoint is not automatically safe: use `.RequireAuthorization(...)` or an explicit anonymous policy as appropriate, and keep the business checks in the service too.

## Part III — Data and business workflows

### Level 4: Entity Framework Core and MySQL

### 14. EF Core, `DbContext`, and entities

**Entity Framework Core (EF Core)** is the .NET object-relational mapper (ORM). It translates LINQ queries and tracked entity changes into database operations. `ApplicationDbContext` derives from `IdentityDbContext<ApplicationUser>`, so it contains both Identity records and the school's `DbSet` properties.

The project uses **Pomelo.EntityFrameworkCore.MySql** to connect EF Core to MySQL 8.4. Pomelo is the database provider; EF Core supplies the common query and change-tracking APIs.

```csharp
var invoice = await db.FeeInvoices
    .AsNoTracking()
    .SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
```

`AsNoTracking()` is useful for read-only results because EF Core does not need to track changes to the loaded entities. For writes, a tracked entity or a newly added entity can be persisted with `SaveChangesAsync()`.

### 15. Relationships, indexes, and constraints

Entities define relationships such as a student having enrollments or an invoice having payment allocations. `ApplicationDbContext.OnModelCreating` configures keys, foreign keys, delete behavior, indexes, and database-specific details.

Application validation gives users useful errors, while database constraints are the final guard against duplicate or orphaned records. A check in C# alone can race when two requests try the same operation at once. Use a transaction and a unique constraint for invariants that must hold under concurrency.

### 16. Migrations and schema changes

An EF Core migration is a versioned description of database schema changes. Migrations are committed with the code and applied in order; EF Core records applied migration IDs in the database. Create a new migration for a new schema change rather than editing one that has already been applied to a shared database.

In this project, Development startup applies checked-in migrations automatically. Production startup deliberately does not; use the explicit migration command during a controlled deployment after reviewing and backing up the database. Migration code is the schema history, while `ApplicationDbContext` is the current model.

### 17. Transactions and multi-step operations

A transaction makes a set of database changes commit together or roll back together. It is important when a business action creates or updates several related records, such as approving an admission and creating its student, enrollment, review, and history records.

Use a transaction at the service-workflow boundary when partial completion would leave invalid state. Transactions do not automatically cover files, SMTP, or payment providers. For external side effects, design for retries, duplicate callbacks, and reconciliation instead of assuming a database rollback can undo another system.

### 18. Amounts, dates, and school rules

Use `decimal` for fee amounts because binary floating-point cannot represent many decimal fractions exactly. Store event timestamps in UTC (`DateTimeOffset` is used in the domain) and convert them using the school's configured timezone when showing dates or calculating local periods. Business rules such as enrollment capacity, marks limits, payment allocation, and approval history belong in services and database constraints where appropriate.

## Part IV — Identity and application security

### Level 5: Sign-in and access rules

### 19. ASP.NET Core Identity

Identity manages user accounts, password hashing, sign-in, security tokens, roles, and related account workflows. This project stores Identity data through its EF Core store in the same MySQL database as school records. The app uses cookie authentication for browser sessions and disables public account registration; staff accounts are created by an authorized administrator.

Identity handles account authentication plumbing, but it does not decide every school-specific permission by itself. The application defines roles and adds business checks for teachers, parents, admission staff, accountants, and reviewers.

### 20. Authentication versus authorization

- **Authentication:** who is signed in?
- **Authorization:** may this account perform this operation on this record?

`[Authorize(Roles = "...")]` can protect a Blazor page. A route or navigation menu that is hidden from a user is not security; the server must reject direct access as well. Services also check record-level rules, such as whether a parent is linked to a student or a teacher is assigned to a class. This prevents a user with a broad role from reading another family's records.

### 21. Validation, authorization, and CSRF

Validation asks whether input is well-formed, such as a mark being in range or an uploaded file meeting allowed size and type rules. Authorization asks whether the current user may perform the requested action. Passing validation does not grant permission.

Cookie-authenticated browsers automatically attach cookies to requests, so state-changing requests need cross-site request forgery (CSRF) protection. ASP.NET Core antiforgery support is enabled in the request pipeline. Cross-Origin Resource Sharing (CORS) controls which browser origins can call an endpoint; it is not a substitute for authorization or CSRF protection.

### 22. Secrets, configuration, and Data Protection

Configuration can come from JSON files, environment variables, command-line settings, and .NET User Secrets during local development. The double underscore form maps environment variables to nested keys; for example, `ConnectionStrings__SchoolManagement` maps to `ConnectionStrings:SchoolManagement`.

Keep connection strings, Identity bootstrap passwords, SMTP passwords, payment merchant values, and certificate passwords out of committed files and logs. In production, this app requires explicit database, allowed-host, trusted-proxy, private-document, and persistent Data Protection key settings. Data Protection keys protect authentication cookies and other framework-protected values; losing the key ring can invalidate existing cookies.

### 23. Trusted proxies and HTTPS

A reverse proxy may terminate TLS and forward the original scheme and client address to ASP.NET Core. The application accepts forwarded headers only from explicitly configured proxy IP addresses. Trusting arbitrary forwarded headers can let clients spoof their source or scheme. Production cookies require HTTPS, and the public host names must be configured rather than allowing every host.

## Part V — Files, external services, and background work

### Level 6: Work beyond a page request

### 24. Hosted services and the email outbox

An `IHostedService` or `BackgroundService` runs work for the lifetime of the web host. The project's hosted worker checks queued email and overdue reminders, sends messages when SMTP is configured, and records job outcomes. The email outbox stores work in the database before a worker tries to deliver it.

An outbox makes delivery retryable and visible, but SMTP is still an external system: delivery can be delayed or fail. The UI should distinguish a queued message from one confirmed as sent, and retries should avoid sending duplicates where possible.

### 25. Private files and downloads

Admission documents and message attachments contain private information. They are stored outside the public web root. A download endpoint checks the signed-in user's role or relationship to the record before returning file bytes. A difficult-to-guess URL is not access control; every request must be authorized on the server.

File uploads should be limited by size, validated by content and allowed format, and stored using generated keys rather than trusting the submitted filename as a filesystem path. Keep original display names separate from private storage paths.

### 26. Payment callbacks and idempotency

A browser redirect from a payment provider is not proof that money was received. The server validates a provider callback against the provider's validation API and the expected transaction, amount, currency, and status. Duplicate callbacks must not allocate the same payment twice. If a provider response is ambiguous, keep the payment available for reconciliation rather than silently treating it as settled.

## Part VI — Testing and runtime

### 27. Test layers

- **Unit test:** checks a focused rule such as grade calculation without starting the web host.
- **Service workflow test:** exercises connected business services with an isolated relational database, such as SQLite.
- **Database startup or migration check:** verifies that migrations and application startup work with the intended database engine.
- **Manual browser walkthrough:** confirms complete user journeys across pages, roles, and interactive behavior.

The repository has unit and service-workflow tests, and CI applies migrations to disposable MySQL and starts the app in Production for health checks. Those automated checks complement manual browser walkthroughs; they do not prove a live deployment or a real SMTP/payment-provider delivery.

### 28. Health checks and production startup

`/health/live` reports that the process can respond. `/health/ready` attempts a database connection and reports unavailable if the database cannot be reached. Liveness and readiness answer different operational questions.

Production startup validates important configuration and does not run migrations automatically. A deployment should prepare persistent storage and keys, take a backup, apply the migration command, start the app behind a trusted TLS reverse proxy, and verify health. A successful build or local health request is not evidence that the app has been deployed to a public server.

# Interview questions with short answers

### Beginner

1. **What is the difference between C# and .NET?** C# is the language; .NET is the runtime, libraries, and SDK used to build and execute applications.
2. **What is a .NET solution?** A file that groups related projects for building and development.
3. **What is dependency injection?** A way for an object to receive the services it needs instead of constructing them directly.
4. **What is a Razor component?** A reusable UI unit that combines Razor markup, C# state, and event handling.
5. **What does EF Core do?** It maps .NET objects and LINQ queries to relational database operations.

### Intermediate

6. **What is the difference between `Task` and `Task<T>`?** `Task` represents asynchronous work with no returned value; `Task<T>` represents asynchronous work that returns a value of type `T`.
7. **Why use a service layer?** It keeps business rules and workflows out of UI event handlers and HTTP endpoints.
8. **What is the difference between an entity and a DTO?** An entity represents persisted domain state; a DTO describes data passed across an application boundary.
9. **What is a migration?** A versioned database schema change that can be applied in a known order.
10. **What is the difference between authentication and authorization?** Authentication identifies the user; authorization checks what the user may do.
11. **Why is `DbContext` short-lived?** It tracks state, is not thread-safe, and is best scoped to a single unit of database work.

### Advanced

12. **What is the difference between middleware and an endpoint?** Middleware wraps broad request processing; an endpoint handles a matched route and produces its response.
13. **How does Interactive Server Blazor work?** Components run on the server and communicate UI events and render updates over a persistent circuit.
14. **Why is a role check not always enough?** A user may have the right role but still lack access to a particular student, class, or payment record.
15. **Why are database constraints needed if services validate input?** Concurrent requests can pass the same application check; database constraints enforce invariants at the storage boundary.
16. **Why doesn't a transaction cover an email or payment provider call?** The external system has its own state and transaction boundary; retries and reconciliation are needed.
17. **What is the difference between liveness and readiness?** Liveness asks whether the process can answer; readiness asks whether it can serve work with required dependencies available.

## Suggested way to study this project

Trace an admission review from `src/SchoolManagement.Web/Components/Pages/Admissions/ReviewQueue.razor` to `src/SchoolManagement.Application/Admissions/IAdmissionService.cs` and `AdmissionService.cs`. Then follow its database access through `IApplicationDbContextFactory` and `src/SchoolManagement.Infrastructure/Persistence/ApplicationDbContext.cs`. This shows how a Blazor event reaches a business workflow and then EF Core without putting SQL or business decisions in the page.

For the full roadmap and architecture, see [the project plan](plan.md). For phase-by-phase implementation details, see the [phase guides](guides/). For setup and run instructions, see the [README](README.md).
