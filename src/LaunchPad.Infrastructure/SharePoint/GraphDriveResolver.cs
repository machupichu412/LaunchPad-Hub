using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

namespace LaunchPad.Infrastructure.SharePoint;

/// <summary>
/// Resolves and caches the drive ID behind SharePoint:SiteId for the process lifetime —
/// shared by GraphFolderProvisioner and GraphDocumentStorage so there's one cache, not two.
/// SharePoint:DriveId can be set to skip the resolve-from-site call entirely (a config
/// tradeoff: saves a Graph call per cold start, but silently stops matching reality if IT
/// ever repoints the site's default document library).
/// </summary>
public sealed class GraphDriveResolver
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _siteId;
    private readonly string? _configuredDriveId;
    private string? _resolvedDriveId;

    public GraphDriveResolver(GraphServiceClient graphClient, IConfiguration configuration)
    {
        _graphClient = graphClient;
        _siteId = configuration["SharePoint:SiteId"] ?? string.Empty;
        _configuredDriveId = configuration["SharePoint:DriveId"];
    }

    public async Task<string> GetDriveIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_configuredDriveId)) return _configuredDriveId;
        if (_resolvedDriveId is not null) return _resolvedDriveId;

        var drive = await _graphClient.Sites[_siteId].Drive.GetAsync(cancellationToken: ct);
        _resolvedDriveId = drive?.Id
            ?? throw new InvalidOperationException($"Could not resolve the default drive for SharePoint site '{_siteId}'.");
        return _resolvedDriveId;
    }
}
