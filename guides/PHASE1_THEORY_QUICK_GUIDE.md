# Phase 1 — Foundation theory and setup

Phase 1 creates the foundation for later school modules: sign-in, roles, the first administrator dashboard, and editable school setup data. It covers the school profile, academic years, classes, and sections. Admissions, students, attendance, exams, and fees remain future phases.

## 1. The four project layers

| Project | Responsibility | Example |
| --- | --- | --- |
| `SchoolManagement.Core` | Domain entities and role names, with no database or web dependency | `AcademicYear`, `Section`, `SchoolRoles` |
| `SchoolManagement.Application` | Use-case services and interfaces that the rest of the app can call | `ISchoolSetupService`, `SchoolSetupService` |
| `SchoolManagement.Infrastructure` | EF Core, MySQL, ASP.NET Core Identity, and service registrations | `ApplicationDbContext`, `UserManagementService` |
| `SchoolManagement.Web` | Blazor pages, authentication middleware, and the HTTP host | Admin pages and `Program.cs` |

The references point inward: the web host uses the application and infrastructure projects, while the core domain stays independent. This keeps database and screen details out of the basic school entities.

## 2. The Phase 1 data model

- **School profile:** one school-wide record stores contact details and a timezone. Its fixed ID makes the single-profile rule visible in the first version.
- **Academic year:** stores a name, date range, and active flag. The application service checks that the end date follows the start date and that names are unique.
- **Class and section:** one class has many sections. A section belongs to one class and has its own capacity. Database indexes prevent duplicate class names, grade levels, and section names within a class.
- **Identity user and user profile:** Identity stores login and security fields; `UserProfile` stores the school-specific full name and phone number. The profile's user ID is also a foreign key to the Identity account.

Entity Framework Core turns the model configuration in `ApplicationDbContext` into MySQL tables, keys, foreign keys, and indexes. The checked-in `InitialPhase1` migration is the versioned set of SQL operations that creates those tables.

## 3. How a saved change reaches MySQL

For school setup pages, the flow is:

1. A Super Admin submits a Blazor form.
2. The page calls `ISchoolSetupService`, rather than writing SQL or using a database context itself.
3. The application service validates the input, creates a context through `IApplicationDbContextFactory`, and saves the entity.
4. EF Core sends parameterized commands to MySQL. The database constraints provide a final check for unique values and valid relationships.
5. The page reloads its list and shows a success or error message.

The factory creates a short-lived `DbContext` for each operation. A context tracks database changes and is not thread-safe, so it should not be shared across unrelated Blazor UI events.

## 4. Sign-in, roles, and account setup

ASP.NET Core Identity handles password hashing, sign-in cookies, failed sign-in counts, and account locking. `SchoolRoles` defines the six project roles. During startup, `InitialDataSeeder` creates any missing roles and the blank school profile.

Public registration is disabled. Configure the first Super Admin locally with .NET User Secrets. That account can then create staff/student accounts, assign roles, and lock or unlock accounts from **User management**. The application prevents an administrator from locking their own account or removing the final active Super Admin role.

The UI hides admin navigation from other roles, and each admin page also has a server-side `[Authorize(Roles = ...)]` check. Hiding a link alone is not access control: the page-level authorization check is what blocks a direct request.

## 5. Configure and run

Create a MySQL 8.4 database and a database user with access to it. From the repository root, set local-only configuration:

```sh
dotnet user-secrets set "ConnectionStrings:SchoolManagement" "Server=localhost;Port=3306;Database=school_management;User ID=school_app;Password=<local-secret>" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:Password" "<choose-a-strong-secret>" --project src/SchoolManagement.Web
dotnet user-secrets set "SeedAdmin:FullName" "School Administrator" --project src/SchoolManagement.Web
```

Replace the placeholders on your computer. Do not put their values in source files, chat, or Git. Then restore and build:

```sh
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln
dotnet run --project src/SchoolManagement.Web
```

When the app runs in Development, it applies the checked-in migration and seeds roles and the optional first administrator. Sign in using that administrator's email and password. If bootstrap administrator settings are omitted, the app still creates the roles, but there is no initial account to sign in with.

The repository includes a local `dotnet-ef` tool manifest. To generate a future migration after changing the model:

```sh
dotnet tool restore
dotnet tool run dotnet-ef migrations add DescribeYourChange --project src/SchoolManagement.Infrastructure --startup-project src/SchoolManagement.Web --output-dir Persistence/Migrations
```

`ApplicationDbContextDesignFactory` supplies provider metadata to EF Core's design-time tools; migration generation does not need a running database. The running Development app uses the configured connection string when it applies migrations.

## 6. A manual Phase 1 walkthrough

After the app starts with a reachable database:

1. Sign in as the bootstrap Super Admin.
2. Save the school's name, contact details, and timezone.
3. Add an academic year, edit it, and make it active.
4. Add a class, then create one or more sections beneath it.
5. Create an account, select its role, and confirm it appears in the account list.
6. Sign in with another role to confirm the admin pages are protected.

The solution was compiled during implementation. Database-backed UI behavior still needs this manual walkthrough against a reachable MySQL server; no MySQL server was running in the implementation environment.
