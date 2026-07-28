using LaunchPad.Api.Authorization;
using LaunchPad.Api.Middleware;
using LaunchPad.Application.Common;
using LaunchPad.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Serilog;

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

    // Fail closed: every endpoint requires auth unless explicitly opted out.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
});

builder.Services.AddScoped<IAuthorizationHandler, OwnsProjectHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnsCandidateProfileHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- Data, repositories, matching engine, redaction mapper ---
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("Sql") ?? string.Empty, name: "sql", tags: new[] { "ready" })
    .AddAzureServiceBusQueue(
        builder.Configuration["ServiceBus:Namespace"] ?? string.Empty,
        builder.Configuration["ServiceBus:QueueName"] ?? string.Empty,
        name: "servicebus",
        tags: new[] { "ready" });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Spa", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

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
