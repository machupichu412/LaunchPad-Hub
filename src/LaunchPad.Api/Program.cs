using FluentValidation;
using LaunchPad.Api.Authorization;
using LaunchPad.Api.LocalDemo;
using LaunchPad.Api.Middleware;
using LaunchPad.Application.Common;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Projects;
using LaunchPad.Application.SharePoint;
using LaunchPad.Infrastructure.DependencyInjection;
using LaunchPad.Infrastructure.Matching;
using LaunchPad.Infrastructure.Persistence;
using LaunchPad.Infrastructure.SharePoint;
using LaunchPad.Infrastructure.Storage;
using LaunchPad.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Web;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// --- Authentication: validate Entra-issued access tokens ---
// Local multi-role testing only (see LocalDemo/DevPersonaAuthHandler.cs). The flag outside
// Development is a misconfiguration serious enough to refuse to start rather than ignore.
var useDevPersonas = builder.Configuration.GetValue<bool>("Auth:UseDevPersonas");
if (useDevPersonas && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Auth:UseDevPersonas is set outside Development. Persona auth must never run in a deployed environment.");
}

var authentication = builder.Services.AddAuthentication(useDevPersonas
    ? DevPersonaAuthHandler.SelectorSchemeName
    : JwtBearerDefaults.AuthenticationScheme);
authentication.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

if (useDevPersonas)
{
    authentication
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevPersonaAuthHandler>(DevPersonaAuthHandler.SchemeName, _ => { })
        .AddPolicyScheme(DevPersonaAuthHandler.SelectorSchemeName, DevPersonaAuthHandler.SelectorSchemeName, o =>
            o.ForwardDefaultSelector = ctx => ctx.Request.Headers.ContainsKey(DevPersonaAuthHandler.HeaderName)
                ? DevPersonaAuthHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme);
}

// --- Authorization: policies, not scattered role strings ---
builder.Services.AddAuthorization(options =>
{
    // Explicit [Authorize] uses DefaultPolicy; missing attributes use FallbackPolicy.
    // Both must reject zero-role, app-only, and malformed-identity principals.
    var applicationUser = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(Roles.Executive, Roles.ProgramOps, Roles.Sponsor, Roles.Candidate, Roles.HiringManager, Roles.SubjectMatterExpert)
        .RequireAssertion(ctx => ApplicationIdentity.IsValid(ctx.User))
        .Build();
    options.DefaultPolicy = applicationUser;
    options.FallbackPolicy = applicationUser;
    options.AddPolicy(Policies.ApplicationUser, applicationUser);

    options.AddPolicy(Policies.ViewTalentPipeline, p =>
        p.RequireRole(Roles.Executive, Roles.ProgramOps, Roles.Sponsor, Roles.HiringManager));

    options.AddPolicy(Policies.ViewHiddenScores, p =>
        p.RequireRole(Roles.Executive, Roles.ProgramOps));

    options.AddPolicy(Policies.ApproveMatch, p =>
        p.RequireRole(Roles.ProgramOps));

    options.AddPolicy(Policies.ApproveProject, p =>
        p.RequireRole(Roles.ProgramOps));

    options.AddPolicy(Policies.ManageOwnProfile, p =>
        p.Requirements.Add(new OwnsCandidateProfileRequirement()));

    options.AddPolicy(Policies.ManageOwnProject, p =>
        p.Requirements.Add(new OwnsProjectRequirement()));

    options.AddPolicy(Policies.ChangeOwnProject, p =>
        p.Requirements.Add(new ChangeProjectRequirement()));

    options.AddPolicy(Policies.ProjectOwnerOnly, p =>
        p.Requirements.Add(new ProjectOwnerRequirement()));

    options.AddPolicy(Policies.ChangeOwnAssignment, p =>
        p.Requirements.Add(new ChangeAssignmentRequirement()));

    options.AddPolicy(Policies.ManageOwnAssignment, p =>
        p.Requirements.Add(new OwnsAssignmentRequirement()));

    // Sponsor included so a project owner can reach AssignmentsController at all — the
    // per-action ManageOwnAssignment resource check (OwnsAssignmentHandler) still scopes
    // them to assignments on their own projects, same defense-in-depth shape as elsewhere.
    options.AddPolicy(Policies.ViewOwnAssignment, p =>
        p.RequireRole(Roles.Candidate, Roles.Sponsor, Roles.ProgramOps, Roles.Executive));

});

builder.Services.AddScoped<IAuthorizationHandler, OwnsProjectHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ChangeProjectHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ProjectOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ChangeAssignmentHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnsCandidateProfileHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnsAssignmentHandler>();

// Admission control precedes authentication; user/workload quotas precede any SQL work.
builder.Services.AddSingleton<RequestAdmissionMiddleware>();
builder.Services.AddApplicationRateLimits(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- Data, repositories, matching engine, redaction mapper ---
builder.Services.AddInfrastructure(builder.Configuration);

// Local-demo-only escape hatch: no SQL Server reachable on this machine, so swap in
// an in-memory DbContext. Gated on IsDevelopment() so it can never activate in a
// deployed environment even if the config flag leaked into a shared settings file.
var useInMemoryForLocalDemo = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Database:UseInMemoryForLocalDemo");

if (useInMemoryForLocalDemo)
{
    builder.Services.RemoveAll<DbContextOptions<LaunchPadDbContext>>();
    builder.Services.RemoveAll<IDbContextOptionsConfiguration<LaunchPadDbContext>>();
    builder.Services.AddDbContext<LaunchPadDbContext>(o => o.UseInMemoryDatabase("launchpad-local-demo"));
}

// Same dev-only opt-in as the in-memory swap above, but for the "LocalFull" profile
// (a real SQL Server container — see docker-compose.homelab.yml) where migrations
// have already been applied and LocalDemoSeeder.Seed's own idempotency check
// (db.Programs.Any()) makes it safe to call on every startup against a
// persistent database, not just a throwaway in-memory one.
var seedOnStartup = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Database:SeedOnStartup");

// Cohort-wide matching normally runs off the request thread via the "matching-jobs"
// Service Bus queue + CohortMatchingFunction. No Service Bus emulator exists in this repo
// (see docker-compose.homelab.yml's Tier 1/2 split), so local/demo runs fall back to
// running the job immediately in-process instead — same dev-only opt-in shape as the DB
// and storage swaps above.
var runMatchingInlineForLocalDemo = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Matching:RunInlineForLocalDemo");

builder.Services.AddScoped<IMatchingJobPublisher>(sp => runMatchingInlineForLocalDemo
    ? sp.GetRequiredService<InlineMatchingJobPublisher>()
    : sp.GetRequiredService<ServiceBusMatchingJobPublisher>());

// Folder provisioning normally runs off the request thread via the "sharepoint-provisioning"
// Service Bus queue + SharePointProvisioningFunction — same dev-only inline fallback shape
// as matching above.
var provisionFoldersInlineForLocalDemo = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("SharePoint:ProvisionInlineForLocalDemo");

builder.Services.AddScoped<IFolderProvisioningJobPublisher>(sp => provisionFoldersInlineForLocalDemo
    ? sp.GetRequiredService<InlineFolderProvisioningJobPublisher>()
    : sp.GetRequiredService<ServiceBusFolderProvisioningJobPublisher>());

// Same "gracefully degrade for local dev" shape as the profile-picture storage swap below: a
// real SharePoint site (SharePoint:SiteId) gets the Graph-backed implementations; this sandbox
// (and any dev box without one configured) falls back to local disk. IFolderProvisioner is also
// consumed by the Functions host (see its own Program.cs) — IDocumentStorage is Api-only, since
// only this host has the deliverable upload/download endpoints.
if (string.IsNullOrWhiteSpace(builder.Configuration["SharePoint:SiteId"]))
{
    builder.Services.AddSingleton<IFolderProvisioner>(sp => sp.GetRequiredService<LocalDiskFolderProvisioner>());
    builder.Services.AddSingleton<IDocumentStorage>(sp => sp.GetRequiredService<LocalDiskDocumentStorage>());
}
else
{
    builder.Services.AddSingleton<IFolderProvisioner>(sp => sp.GetRequiredService<GraphFolderProvisioner>());
    builder.Services.AddSingleton<IDocumentStorage>(sp => sp.GetRequiredService<GraphDocumentStorage>());
}

// Same "gracefully degrade for local dev" shape as the DB provider swap above: a
// real storage account (Storage:AccountUrl) gets the Blob-backed implementation;
// this sandbox (and any dev box without one provisioned) falls back to local disk.
if (string.IsNullOrWhiteSpace(builder.Configuration["Storage:AccountUrl"]))
{
    builder.Services.AddSingleton<IProfilePictureStorage, LocalDiskProfilePictureStorage>();
    builder.Services.AddSingleton<ICommunityImageStorage, LocalDiskCommunityImageStorage>();
}
else
{
    builder.Services.AddSingleton<IProfilePictureStorage, BlobProfilePictureStorage>();
    builder.Services.AddSingleton<ICommunityImageStorage, BlobCommunityImageStorage>();
}

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<LargeCollectionResultFilter>();
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1024 * 1024));
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(Policies.ApplicationUser));
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();

// Unhandled exceptions produced a bare Kestrel 500 with no body; these give every
// failure the same ProblemDetails shape the validation failures already use.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// DTO payloads are JSON and highly compressible — the talent pipeline and project list
// responses are the largest. Brotli first, gzip for anything that can't take it.
builder.Services.AddResponseCompression(options =>
{
    // Only over HTTPS here, which is all this API serves (httpsOnly in appService.bicep);
    // the BREACH concern that makes this opt-in doesn't apply to bearer-token auth with
    // no secrets reflected into response bodies.
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
});

// A global ceiling behind the existing per-endpoint [RequestSizeLimit]s. Defaults to the
// largest of them (deliverable upload) so it backstops rather than overrides.
var maxRequestBodyBytes = builder.Configuration.GetValue<long?>("Api:MaxRequestBodyBytes") ?? 110L * 1024 * 1024;
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = maxRequestBodyBytes);

var healthChecks = builder.Services.AddHealthChecks();
if (!useInMemoryForLocalDemo)
{
    healthChecks
        .AddSqlServer(builder.Configuration.GetConnectionString("Sql") ?? string.Empty, name: "sql", tags: new[] { "ready" })
        .AddAzureServiceBusQueue(
            builder.Configuration["ServiceBus:Namespace"] ?? string.Empty,
            builder.Configuration["ServiceBus:QueueName"] ?? string.Empty,
            new Azure.Identity.DefaultAzureCredential(),
            configure: options => options.UsePeekMode = false,
            timeout: TimeSpan.FromSeconds(5),
            name: "servicebus",
            tags: new[] { "ready" });
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Spa", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Without this, browsers hide WWW-Authenticate from fetch()'s Response object
        // even when the API sends it — the exact validation failure reason (invalid
        // audience, expired token, etc.) becomes invisible client-side.
        .WithExposedHeaders("WWW-Authenticate", "Retry-After", "X-Correlation-Id"));
});

var app = builder.Build();

if (useInMemoryForLocalDemo || seedOnStartup)
{
    using var seedScope = app.Services.CreateScope();
    LocalDemoSeeder.Seed(seedScope.ServiceProvider.GetRequiredService<LaunchPadDbContext>());
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
// After the correlation id so the handler can put it in the problem body; in Development
// UseDeveloperExceptionPage is registered above and wins, keeping stack traces local.
app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseMiddleware<RequestAdmissionMiddleware>();
app.UseResponseCompression();
app.UseCors("Spa");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<AppUserProvisioningMiddleware>();

app.MapControllers();

app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous().DisableRateLimiting();

app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Readiness);

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the app in integration tests.
public partial class Program { }
