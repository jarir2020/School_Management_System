using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Web.Components;
using SchoolManagement.Web.Components.Account;
using SchoolManagement.Infrastructure;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Seeding;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Finance;
using SchoolManagement.Application.Administration;
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Enums;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var trustedProxyAddresses = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
if (!builder.Environment.IsDevelopment() && trustedProxyAddresses.Length == 0)
{
    throw new InvalidOperationException("Configure at least one trusted reverse proxy in ForwardedHeaders:KnownProxies for production.");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var proxyAddress in trustedProxyAddresses)
    {
        if (!IPAddress.TryParse(proxyAddress, out var address))
        {
            throw new InvalidOperationException("ForwardedHeaders:KnownProxies must contain valid IP addresses.");
        }

        options.KnownProxies.Add(address);
    }
});

var dataProtectionKeyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
var dataProtectionCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
var dataProtectionCertificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
var dataProtectionCertificatePasswordFile = builder.Configuration["DataProtection:CertificatePasswordFile"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePasswordFile))
{
    if (!Path.IsPathFullyQualified(dataProtectionCertificatePasswordFile))
    {
        throw new InvalidOperationException("DataProtection:CertificatePasswordFile must be an absolute path.");
    }

    dataProtectionCertificatePassword = File.ReadAllText(dataProtectionCertificatePasswordFile).TrimEnd('\r', '\n');
}

if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(dataProtectionKeyDirectory) || !Path.IsPathFullyQualified(dataProtectionKeyDirectory)))
{
    throw new InvalidOperationException("Set DataProtection:KeyDirectory to an absolute persistent private directory in production.");
}

if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(dataProtectionCertificatePath)
        || !Path.IsPathFullyQualified(dataProtectionCertificatePath)
        || string.IsNullOrWhiteSpace(dataProtectionCertificatePassword)))
{
    throw new InvalidOperationException("Set DataProtection:CertificatePath and either CertificatePassword or CertificatePasswordFile so production keys are encrypted at rest.");
}

if (!string.IsNullOrWhiteSpace(dataProtectionKeyDirectory) || !string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    var dataProtectionBuilder = builder.Services.AddDataProtection().SetApplicationName("SchoolManagement");
    if (!string.IsNullOrWhiteSpace(dataProtectionKeyDirectory))
    {
        var keyDirectory = Path.GetFullPath(dataProtectionKeyDirectory, builder.Environment.ContentRootPath);
        Directory.CreateDirectory(keyDirectory);
        dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
    }

    if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
    {
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            dataProtectionCertificatePath,
            dataProtectionCertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
        dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
    }
}

var privateDocumentRoot = builder.Configuration["Storage:PrivateDocumentRoot"];
if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(privateDocumentRoot) || !Path.IsPathFullyQualified(privateDocumentRoot)))
{
    throw new InvalidOperationException("Set Storage:PrivateDocumentRoot to an absolute private directory in production.");
}

var allowedHosts = builder.Configuration["AllowedHosts"];
if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*"))
{
    throw new InvalidOperationException("Set AllowedHosts to the public host names served by this production instance.");
}

builder.Services.AddSchoolManagementInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<IEmailSender<ApplicationUser>, QueuedIdentityEmailSender>();
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .AllowAnonymous();

app.MapGet("/health/ready", async (
        IDbContextFactory<ApplicationDbContext> contextFactory,
        CancellationToken cancellationToken) =>
    {
        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? Results.Ok(new { status = "ready" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    })
    .AllowAnonymous();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> admissionStaffPolicy = policy =>
    policy.RequireRole(SchoolRoles.SuperAdmin, SchoolRoles.AdmissionOfficer);

app.MapGet("/admin/admissions/documents/{documentId:int}", async (
        HttpContext http,
        int documentId,
        IAdmissionService admissions,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var document = await admissions.GetDocumentAsync(actorUserId, documentId, cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.File(document.Content, document.ContentType, Path.GetFileName(document.FileName));
    })
    .RequireAuthorization(admissionStaffPolicy);

app.MapGet("/admin/students/export.csv", async (
        HttpContext http,
        string? search,
        StudentStatus? status,
        int? academicYearId,
        int? sectionId,
        IStudentDirectoryService students,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var export = await students.ExportCsvAsync(new StudentSearchFilter(
            search,
            status,
            academicYearId,
            sectionId), actorUserId, cancellationToken);
        return Results.File(export.Content, export.ContentType, export.FileName);
    })
    .RequireAuthorization(admissionStaffPolicy);

app.MapGet("/admin/students/export.xlsx", async (
        HttpContext http,
        string? search,
        StudentStatus? status,
        int? academicYearId,
        int? sectionId,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var file = await phaseSix.ExportStudentsExcelAsync(
            new StudentSearchFilter(search, status, academicYearId, sectionId), actorUserId, cancellationToken);
        return Results.File(file.Content, file.ContentType, file.FileName);
    })
    .RequireAuthorization(admissionStaffPolicy);

app.MapGet("/finance/export.xlsx", async (
        HttpContext http,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var file = await phaseSix.ExportFinanceReportExcelAsync(actorUserId, cancellationToken);
        return Results.File(file.Content, file.ContentType, file.FileName);
    })
    .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.SuperAdmin, SchoolRoles.Accountant));

app.MapGet("/admin/students/import-template.xlsx", async (
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var file = await phaseSix.CreateStudentImportTemplateAsync(cancellationToken);
        return Results.File(file.Content, file.ContentType, file.FileName);
    })
    .RequireAuthorization(admissionStaffPolicy);

app.MapGet("/teacher/examinations/{examSubjectId:int}/marks-template.xlsx", async (
        HttpContext http,
        int examSubjectId,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var teacherUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var file = await phaseSix.CreateMarksImportTemplateAsync(teacherUserId, examSubjectId, cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    })
    .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.Teacher));

app.MapGet("/admin/students/documents.zip", async (
        HttpContext http,
        string studentIds,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var ids = studentIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .ToArray();
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var file = await phaseSix.ExportStudentDocumentsZipAsync(actorUserId, ids, cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
    })
    .RequireAuthorization(admissionStaffPolicy);

app.MapGet("/finance/invoices/{invoiceId:int}/pdf", async (
        HttpContext http,
        int invoiceId,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var file = await phaseSix.RenderInvoicePdfAsync(actorUserId, invoiceId, cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    })
    .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.SuperAdmin, SchoolRoles.Accountant, SchoolRoles.ParentStudent));

app.MapGet("/finance/receipts/{paymentId:int}/pdf", async (
        HttpContext http,
        int paymentId,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var file = await phaseSix.RenderReceiptPdfAsync(actorUserId, paymentId, cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    })
    .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.SuperAdmin, SchoolRoles.Accountant, SchoolRoles.ParentStudent));

app.MapGet("/parent/results/{studentId:int}/{examinationId:int}/pdf", async (
        HttpContext http,
        int studentId,
        int examinationId,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var file = await phaseSix.RenderReportCardPdfAsync(actorUserId, studentId, examinationId, cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    })
    .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.ParentStudent));

app.MapGet("/parent/messages/attachments/{attachmentId:int}", async (
        HttpContext http,
        int attachmentId,
        IPhaseSixService phaseSix,
        CancellationToken cancellationToken) =>
    {
        var actorUserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var file = await phaseSix.GetMessageAttachmentAsync(actorUserId, attachmentId, cancellationToken);
            return file is null
                ? Results.NotFound()
                : Results.File(file.Content, file.ContentType, Path.GetFileName(file.FileName));
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
    })
    .RequireAuthorization(policy => policy.RequireRole(SchoolRoles.ParentStudent, SchoolRoles.Teacher));

app.MapPost("/payments/sslcommerz/success", async (
        HttpRequest request,
        IFinanceService finance,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var outcome = await finance.ProcessGatewayPaymentAsync(new GatewayCallbackValues(
                form["tran_id"].ToString(),
                form["val_id"].ToString()), cancellationToken);
            var result = outcome switch
            {
                GatewayPaymentOutcome.Processed or GatewayPaymentOutcome.AlreadyProcessed => "processed",
                GatewayPaymentOutcome.ReviewRequired => "review",
                _ => "pending"
            };
            return Results.Redirect($"/parent/fees?payment={result}");
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            // Do not expose provider response details or transaction credentials on a public callback.
            return Results.Redirect("/parent/fees?payment=pending");
        }
    })
    .AllowAnonymous();

app.MapPost("/payments/sslcommerz/ipn", async (
        HttpRequest request,
        IFinanceService finance,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var outcome = await finance.ProcessGatewayPaymentAsync(new GatewayCallbackValues(
                form["tran_id"].ToString(),
                form["val_id"].ToString()), cancellationToken);
            return outcome == GatewayPaymentOutcome.Ignored
                ? Results.BadRequest("Payment validation was not accepted.")
                : Results.Ok("OK");
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return Results.BadRequest("Payment validation could not be completed.");
        }
    })
    .AllowAnonymous();

app.MapPost("/payments/sslcommerz/fail", () => Results.Redirect("/parent/fees?payment=failed"))
    .AllowAnonymous();

app.MapPost("/payments/sslcommerz/cancel", () => Results.Redirect("/parent/fees?payment=pending"))
    .AllowAnonymous();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Development applies new migrations automatically. Production migrations stay an explicit deployment step.
await using (var scope = app.Services.CreateAsyncScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var dbContext = await contextFactory.CreateDbContextAsync();
    if (app.Environment.IsDevelopment())
    {
        await dbContext.Database.MigrateAsync();
    }

    await scope.ServiceProvider.GetRequiredService<InitialDataSeeder>().SeedAsync();
}

app.Run();
