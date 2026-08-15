using System.Collections.Concurrent;
using LaunchPad.Application.SharePoint;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Replaces IFolderProvisioner in tests — no real Graph/local-disk calls, just deterministic
/// fake IDs so FolderProvisioningRunner (run inline by FakeFolderProvisioningJobPublisher) can
/// write SharePointFolderId/WebUrl back onto the entity the same way the real thing would.
/// Also records every path requested for tests that want to assert on provisioning calls.
/// </summary>
public sealed class FakeFolderProvisioner : IFolderProvisioner
{
    public ConcurrentBag<string> RequestedPaths { get; } = new();

    public Task<(string FolderId, string? WebUrl)> EnsureCohortFolderAsync(string cohortName, CancellationToken ct = default) =>
        Ensure($"Cohort:{cohortName}");

    public Task<(string FolderId, string? WebUrl)> EnsureCandidateFolderAsync(
        string cohortName, string candidateDisplayName, CancellationToken ct = default) =>
        Ensure($"Candidate:{cohortName}/{candidateDisplayName}");

    public Task<(string FolderId, string? WebUrl)> EnsureProjectFolderAsync(
        string cohortName, string projectName, CancellationToken ct = default) =>
        Ensure($"Project:{cohortName}/{projectName}");

    private Task<(string FolderId, string? WebUrl)> Ensure(string key)
    {
        RequestedPaths.Add(key);
        return Task.FromResult<(string, string?)>(($"fake-folder:{key}", $"https://fake.sharepoint.local/{Uri.EscapeDataString(key)}"));
    }
}
