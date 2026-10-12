using Microsoft.AspNetCore.Components.Authorization;
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
using SchoolManagement.Core.Authorization;
using SchoolManagement.Core.Enums;

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

builder.Services.AddSchoolManagementInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder> admissionStaffPolicy = policy =>
    policy.RequireRole(SchoolRoles.SuperAdmin, SchoolRoles.AdmissionOfficer);

app.MapGet("/admin/admissions/documents/{documentId:int}", async (
        int documentId,
        IAdmissionService admissions,
        CancellationToken cancellationToken) =>
    {
        var document = await admissions.GetDocumentAsync(documentId, cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.File(document.Content, document.ContentType, Path.GetFileName(document.FileName));
    })
    .RequireAuthorization(admissionStaffPolicy);

app.MapGet("/admin/students/export.csv", async (
        string? search,
        StudentStatus? status,
        int? academicYearId,
        int? sectionId,
        IStudentDirectoryService students,
        CancellationToken cancellationToken) =>
    {
        var export = await students.ExportCsvAsync(new StudentSearchFilter(
            search,
            status,
            academicYearId,
            sectionId), cancellationToken);
        return Results.File(export.Content, export.ContentType, export.FileName);
    })
    .RequireAuthorization(admissionStaffPolicy);

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
