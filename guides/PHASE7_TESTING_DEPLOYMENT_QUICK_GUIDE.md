# Phase 7 — Testing and Deployment Quick Guide

This guide shows the local test commands and a Linux Docker Compose deployment path for the school management app. It prepares the application for a deployment review; it does not mean a live server has been configured or deployed.

## 1. What is covered

- `GradeCalculatorTests` covers grade thresholds, rounded percentages, pass-mark checks, and invalid mark ranges.
- `SchoolWorkflowTests` uses an isolated SQLite database to cover duplicate admission approval and section capacity, teacher authorization and mark limits, underpaid gateway callbacks, and duplicate verified callbacks.
- `.github/workflows/ci.yml` restores the solution, reports vulnerable direct/transitive NuGet packages, builds and tests, applies every EF migration to a disposable MySQL 8.4 service, and starts the app in Production to check `/health/live` and `/health/ready`.
- `/health/live` confirms the web process can answer. `/health/ready` returns success only when the configured database can be reached. Neither endpoint returns database error details.

The SQLite workflow tests exercise application services and relational transactions. CI checks MySQL migrations and startup, but the browser workflows still need a manual walkthrough. The full migration chain has since been applied to the local `school_management` database; six migration records and 49 tables were verified.

Run local verification from the repository root:

```sh
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln --configuration Release
dotnet test SchoolManagement.sln --configuration Release
```

The test project uses xUnit v3's Microsoft Testing Platform runner. A local restore needs access to NuGet. CI uses a disposable database and disposable test credentials; do not copy its connection string into a real environment.

`global.json` selects Microsoft Testing Platform for the .NET 10 SDK. With .NET 10, use `dotnet test --solution SchoolManagement.sln --configuration Release`; if the machine has only the .NET 10 runtime installed for these .NET 9 targets, prefix the command with `DOTNET_ROLL_FORWARD=Major`. The documented `dotnet test SchoolManagement.sln` form is for the .NET 9 SDK. See [Microsoft's test runner guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test).

## 2. Production configuration

The application intentionally does not run migrations automatically outside Development. In Production it requires:

- `ConnectionStrings__SchoolManagement` for MySQL.
- `AllowedHosts` with the public host name(s), not `*`.
- `ForwardedHeaders__KnownProxies__0` (and further numbered values if needed) containing the exact IP address of each trusted reverse proxy.
- `Storage__PrivateDocumentRoot` set to an absolute, private, persistent directory.
- `DataProtection__KeyDirectory` set to an absolute, private, persistent directory.
- `DataProtection__CertificatePath` plus either `DataProtection__CertificatePassword` or `DataProtection__CertificatePasswordFile` so the persisted key ring is encrypted at rest.

The app processes forwarded `X-Forwarded-For` and `X-Forwarded-Proto` headers only from those configured IP addresses. Keep the app bound to loopback behind a TLS reverse proxy. Production Identity cookies are HttpOnly and require HTTPS. The Data Protection keys must persist across container recreation so existing authentication cookies can be read; the app encrypts the key ring with the configured PKCS#12 certificate. Retain that certificate for as long as its keys may need to be decrypted, restrict its file and password to the app account, and include protected copies in the recovery plan.

Use a database account scoped to the application database. Keep all credentials in a deployment secret store or a mode-600 local `.env` file, never in tracked files, command history, issue text, or logs. Use long random values for the MySQL application and root passwords. Configure SMTP and SSLCOMMERZ secrets separately if those features will be enabled.

## 3. Linux Docker Compose preparation

Install Docker Engine, the Docker Compose plugin, .NET 9 SDK on the deployment host for the migration tool, MySQL client tools for backups, and a TLS reverse proxy such as Nginx or Caddy. Review the network subnet in `compose.yaml` and change it if it overlaps with the host or an existing Docker network.

Create the untracked environment file and replace every sample value:

```sh
cp .env.example .env
chmod 600 .env
openssl rand -hex 32
```

Generate different random values for `MYSQL_PASSWORD` and `MYSQL_ROOT_PASSWORD`. Set `ALLOWED_HOSTS` to the public DNS name, and set `TRUSTED_PROXY_IP` to the actual IP address seen by the app for the reverse proxy. In this example Compose network the gateway is `172.29.0.1`; verify that value on the host before relying on it.

Create or obtain a PKCS#12 certificate for Data Protection. Set `DATAPROTECTION_CERT_FILE` to the host path of the `.pfx` file and `DATAPROTECTION_CERT_PASSWORD_FILE` to a separate protected file containing its password. For the Compose container's UID 1654, make those files readable only to root and the app group (for example, owner `root:1654`, mode `0440`). Do not commit either file. Set the initial `SEED_ADMIN_EMAIL` and a strong `SEED_ADMIN_PASSWORD` before first startup, then remove the password from `.env` after the administrator account is created.

Start MySQL and wait until it is healthy:

```sh
docker compose up -d --wait mysql
```

Apply migrations from the host with the application database user's connection string. The `.env` values shown here are expected to be shell-safe random hexadecimal values:

```sh
set -a
. ./.env
set +a
export ConnectionStrings__SchoolManagement="Server=127.0.0.1;Port=${MYSQL_PORT:-3307};Database=${MYSQL_DATABASE};User ID=${MYSQL_USER};Password=${MYSQL_PASSWORD};"
./ops/migrate-database.sh
unset ConnectionStrings__SchoolManagement
```

Only start the app after the migration command succeeds:

```sh
docker compose up -d --build web
curl --fail http://127.0.0.1:${APP_PORT:-8080}/health/live
curl --fail http://127.0.0.1:${APP_PORT:-8080}/health/ready
```

The app and database ports bind to loopback. Configure the host reverse proxy to accept public HTTPS and forward to `127.0.0.1:8080`, preserve the incoming `Host`, and set the `X-Forwarded-For` and `X-Forwarded-Proto` headers. For Nginx with Blazor Server, also enable HTTP/1.1 WebSocket upgrade headers and a long-lived upstream read timeout. Allow inbound 80/443 only as required by the TLS setup; do not open the MySQL port to the public network.

## 4. Database migration and rollback

The repository pins `dotnet-ef` to 9.0.20 in `.config/dotnet-tools.json`. `ops/migrate-database.sh` refuses to run if `ConnectionStrings__SchoolManagement` is missing, restores the local tool, then applies the migrations in order. Take a database backup before production migrations and review the generated migration SQL for data-sensitive changes. A database restore is the rollback path for destructive schema changes; do not treat a previous application image as a database rollback.

The current project targets .NET 9, whose official support ends November 10, 2026. Entity Framework Core 9 shares that end-of-support date. The currently selected Pomelo provider is for EF Core 9; verify a stable compatible provider release and complete an upgrade/migration rehearsal before a production release. Do not change only the target framework while retaining an incompatible EF provider. See the [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [EF Core support policy](https://learn.microsoft.com/en-us/ef/core/what-is-new/), and [Pomelo provider repository](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql).

## 5. Database backups and restore drills

Create a MySQL client option file outside the repository. Restrict it to the deployment account:

```ini
[client]
user=school_app
password=REPLACE_WITH_THE_DATABASE_PASSWORD
```

```sh
chmod 600 /path/to/mysql-backup.cnf
MYSQL_DATABASE=school_management \
MYSQL_CNF=/path/to/mysql-backup.cnf \
MYSQL_HOST=127.0.0.1 \
MYSQL_PORT=3307 \
BACKUP_DIR=/srv/school-management-backups \
./ops/backup-mysql.sh
```

The script uses a consistent InnoDB dump, includes routines/triggers/events, compresses the output, sets restrictive file permissions, validates the gzip stream, and only publishes the completed file. The backup directory must be outside the web root and protected because it contains student and financial data. Copy completed backups to encrypted off-host storage and apply a documented retention schedule.

The SQL dump does not contain private admission/message attachments or Data Protection keys. Back up the `private_documents` and `data_protection_keys` volumes with a protected, versioned volume-backup process as well. Keep a secure recovery copy of the Data Protection certificate and its password separately. Confirm the named volume paths with `docker volume ls`; test recovering a matching database dump, document volume, key volume, and certificate together in an isolated staging environment.

For a database-only restore drill, restore into a newly created disposable database rather than overwriting the current production database:

```sh
gzip --decompress --stdout /path/to/school_management-YYYYMMDDTHHMMSSZ.sql.gz \
  | mysql --defaults-extra-file=/path/to/mysql-backup.cnf \
      --host=127.0.0.1 --port=3307 school_management_restore_test
```

Verify the restored schema and representative record counts without copying real student details into logs or screenshots. Delete the disposable database only after recording the drill result and confirming its name. Schedule any real production restore as an approved maintenance operation with a current backup and a written recovery point.

## 6. Manual end-to-end walkthrough

Use synthetic records and a non-production database. Record pass/fail and any defects without including student personal information.

1. Submit an anonymous admission application with a sample document. Verify it appears in the staff queue, that an unauthorized role cannot review it or download its private file, and that the authorized decision creates one student/enrollment.
2. Fill a section to its configured capacity and confirm a further approval is rejected without another enrollment.
3. Assign a teacher to one section and subject. Confirm the teacher can enter valid marks only for that assignment, cannot access another teacher's exam, cannot exceed full marks, and must submit a complete active roster.
4. Review and publish the results. Confirm a parent account sees only its linked child's report card, and a correction requires the authorized reasoned revision workflow.
5. Create a fee assignment and invoice, record a manual payment, and verify one allocation and receipt. In a separate SSLCOMMERZ sandbox, check a successful callback, an underpaid/at-risk callback, and a repeated callback; only a validated matching payment should be allocated.
6. Confirm the parent cannot read another student's invoice, receipt, message, attachment, or report card. Confirm staff-only exports and document ZIPs reject unauthorized roles.
7. Restart the containers and verify the app, database, private documents, and authentication data remain available. Confirm the reverse proxy terminates TLS and `/health/ready` becomes unhealthy when the database is stopped.

## 7. Current completion boundary

The repository includes CI, deployment files, migration and backup helpers, and automated service-level tests. The 10 local unit and workflow tests passed with the .NET 10 MTP runner, and the NuGet audit reported no vulnerable packages. A disposable MySQL 8.4 database accepted the full migration chain. The local `school_management` database has all six migrations applied; the Production health smoke check returned `ready` and `live`. The private backup passed gzip validation and restored to a disposable database with all six migrations and 49 tables verified; that restore database and temporary backup were removed. The hosted GitHub Actions run has not been verified after push. Browser walkthroughs and a live Linux/cloud deployment must be verified in their target environments before claiming production readiness.
