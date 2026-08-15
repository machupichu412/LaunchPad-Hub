using LaunchPad.Application.SharePoint;
using LaunchPad.Infrastructure.DependencyInjection;
using LaunchPad.Infrastructure.SharePoint;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddInfrastructure(context.Configuration);
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Same config-gated Graph-vs-local-disk selection as the Api host's Program.cs.
        // FolderProvisioningRunner (consumed by SharePointProvisioningFunction) needs
        // IFolderProvisioner; this host has no upload/download endpoints, so unlike Api it
        // never needs IDocumentStorage.
        if (string.IsNullOrWhiteSpace(context.Configuration["SharePoint:SiteId"]))
        {
            services.AddSingleton<IFolderProvisioner>(sp => sp.GetRequiredService<LocalDiskFolderProvisioner>());
        }
        else
        {
            services.AddSingleton<IFolderProvisioner>(sp => sp.GetRequiredService<GraphFolderProvisioner>());
        }
    })
    .Build();

host.Run();
