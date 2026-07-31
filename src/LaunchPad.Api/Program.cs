using FluentValidation;
using LaunchPad.Api.Authorization;
using LaunchPad.Api.LocalDemo;
using LaunchPad.Api.Middleware;
using LaunchPad.Application.Common;
using LaunchPad.Application.Projects;
using LaunchPad.Infrastructure.DependencyInjection;
using LaunchPad.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// --- Authorization: policies, not scattered role strings ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ViewTalentPipeline, p =>
        p.RequireRole(Roles.Executive, Roles.ProgramOps, Roles.Sponsor, Roles.HiringManager));

    options.AddPolicy(Policies.ViewHiddenScores, p =>
        p.RequireRole(Roles.Executive, Roles.ProgramOps));

    options.AddPolicy(Policies.ApproveMatch, p =>
        p.RequireRole(Roles.ProgramOps));

    options.AddPolicy(Policies.ManageOwnProfile, p =>
        p.Requirements.Add(new OwnsCandidateProfileRequirement()));

    options.AddPolicy(Policies.ManageOwnProject, p =>
        p.Requirements.Add(new OwnsProjectRequirement()));

    options.AddPolicy(Policies.ManageOwnAssignment, p =>
        p.Requirements.Add(new OwnsAssignmentRequirement()));

    options.AddPolicy(Policies.ViewOwnAssignment, p =>
        p.RequireRole(Roles.Candidate, Roles.ProgramOps, Roles.Executive));

    // Fail closed: every endpoint requires auth unless explicitly opted out.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
});

builder.Services.AddScoped<IAuthorizationHandler, OwnsProjectHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnsCandidateProfileHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnsAssignmentHandler>();

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

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();

var healthChecks = builder.Services.AddHealthChecks();
if (!useInMemoryForLocalDemo)
{
    healthChecks
        .AddSqlServer(builder.Configuration.GetConnectionString("Sql") ?? string.Empty, name: "sql", tags: new[] { "ready" })
        .AddAzureServiceBusQueue(
            builder.Configuration["ServiceBus:Namespace"] ?? string.Empty,
            builder.Configuration["ServiceBus:QueueName"] ?? string.Empty,
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
        .WithExposedHeaders("WWW-Authenticate"));
});

var app = builder.Build();

if (useInMemoryForLocalDemo)
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
app.UseCors("Spa");
app.UseAuthentication();
app.UseMiddleware<AppUserProvisioningMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the app in integration tests.
public partial class Program { }
