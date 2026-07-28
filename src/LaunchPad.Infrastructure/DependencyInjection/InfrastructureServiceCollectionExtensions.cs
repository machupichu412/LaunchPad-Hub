using LaunchPad.Application.Assignments;
using LaunchPad.Application.Candidates;
using LaunchPad.Application.Matching;
using LaunchPad.Application.Projects;
using LaunchPad.Application.Reporting;
using LaunchPad.Application.Reviews;
using LaunchPad.Infrastructure.Persistence;
using LaunchPad.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton<ICandidateDtoMapper, CandidateDtoMapper>();
        services.AddSingleton<IMatchingEngine, MatchingEngine>();

        return services;
    }
}
