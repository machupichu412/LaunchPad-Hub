using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Common;
using LaunchPad.Application.Community;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Notifications;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Reporting;
using LaunchPad.Application.Reviews;
using LaunchPad.Application.SharePoint;
using LaunchPad.Application.Skills;
using LaunchPad.Application.Sponsors;
using LaunchPad.Infrastructure.Matching;
using LaunchPad.Infrastructure.Notifications;
using LaunchPad.Infrastructure.Persistence;
using LaunchPad.Infrastructure.Persistence.Repositories;
using LaunchPad.Infrastructure.SharePoint;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace LaunchPad.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LaunchPadDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Sql"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<ISponsorRepository, SponsorRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<ICommunityRepository, CommunityRepository>();
        services.AddScoped<ICohortRepository, CohortRepository>();
        services.AddScoped<IOpsDashboardRepository, OpsDashboardRepository>();
        services.AddScoped<IProjectInterestRepository, ProjectInterestRepository>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddSingleton<ICandidateDtoMapper, CandidateDtoMapper>();
        services.AddSingleton<IMatchingEngine, MatchingEngine>();
        services.AddSingleton<ITextSimilarityScorer, TfIdfCosineTextSimilarityScorer>();
        services.AddScoped<ICohortMatchingRunner, CohortMatchingRunner>();

        // ServiceBusNotificationPublisher stays registered under its own concrete type —
        // CompositeNotificationPublisher (the actual INotificationPublisher) wraps it to
        // add an always-on in-app Notification row alongside the best-effort async email.
        services.AddScoped<ServiceBusNotificationPublisher>();
        services.AddScoped<INotificationPublisher, CompositeNotificationPublisher>();
        services.AddScoped<IEmailNotifier, GraphEmailNotifier>();

        // Both IMatchingJobPublisher implementations stay registered under their own
        // concrete types — Api's Program.cs picks between them via a config gate (same
        // pattern as Database:UseInMemoryForLocalDemo), since the choice is environment-
        // specific, not something Infrastructure itself should decide.
        services.AddScoped<ServiceBusMatchingJobPublisher>();
        services.AddScoped<InlineMatchingJobPublisher>();

        // GraphServiceClient is safe to always register — constructing it (and the
        // DefaultAzureCredential behind it) makes no network call; only actually calling it
        // does. One shared client instance backs all three Graph-backed SharePoint classes.
        services.AddSingleton(_ => new GraphServiceClient(
            new DefaultAzureCredential(), new[] { "https://graph.microsoft.com/.default" }));
        services.AddSingleton<GraphDriveResolver>();

        // Scoped, not Singleton — it depends on the Scoped repositories (which hold a
        // Scoped DbContext), same lifetime as ICohortMatchingRunner/CohortMatchingRunner.
        services.AddScoped<IFolderProvisioningRunner, FolderProvisioningRunner>();

        // Both IFolderProvisioner and both IDocumentStorage implementations stay registered
        // under their own concrete types — the config-gated selection lives in each host's
        // own Program.cs (Api AND Functions both consume IFolderProvisioner; only Api
        // consumes IDocumentStorage — see the plan for why).
        services.AddSingleton<GraphFolderProvisioner>();
        services.AddSingleton<LocalDiskFolderProvisioner>();
        services.AddSingleton<GraphDocumentStorage>();
        services.AddSingleton<LocalDiskDocumentStorage>();

        // Same "publisher concrete types registered, host Program.cs picks" shape as the
        // matching-job publishers above.
        services.AddScoped<ServiceBusFolderProvisioningJobPublisher>();
        services.AddScoped<InlineFolderProvisioningJobPublisher>();

        return services;
    }
}
