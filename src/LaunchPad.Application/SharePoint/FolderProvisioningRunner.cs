using LaunchPad.Application.Candidates;
using LaunchPad.Application.Cohorts;
using LaunchPad.Application.Projects;

namespace LaunchPad.Application.SharePoint;

public sealed class FolderProvisioningRunner : IFolderProvisioningRunner
{
    private readonly ICohortRepository _cohorts;
    private readonly ICandidateRepository _candidates;
    private readonly IProjectRepository _projects;
    private readonly IFolderProvisioner _folderProvisioner;

    public FolderProvisioningRunner(
        ICohortRepository cohorts, ICandidateRepository candidates, IProjectRepository projects, IFolderProvisioner folderProvisioner)
    {
        _cohorts = cohorts;
        _candidates = candidates;
        _projects = projects;
        _folderProvisioner = folderProvisioner;
    }

    public async Task RunAsync(FolderProvisioningJob job, CancellationToken ct = default)
    {
        switch (job.TargetType)
        {
            case FolderProvisioningTargetType.Cohort:
                await RunCohortAsync(job.TargetId, ct);
                break;
            case FolderProvisioningTargetType.Candidate:
                await RunCandidateAsync(job.TargetId, ct);
                break;
            case FolderProvisioningTargetType.Project:
                await RunProjectAsync(job.TargetId, ct);
                break;
        }
    }

    private async Task RunCohortAsync(int cohortId, CancellationToken ct)
    {
        var cohort = await _cohorts.GetByIdAsync(cohortId, ct);
        if (cohort is null) return;

        var (folderId, webUrl) = await _folderProvisioner.EnsureCohortFolderAsync(cohort.Name, ct);
        cohort.SharePointFolderId = folderId;
        cohort.SharePointFolderWebUrl = webUrl;
        await _cohorts.SaveChangesAsync(ct);
    }

    private async Task RunCandidateAsync(int candidateId, CancellationToken ct)
    {
        var candidate = await _candidates.GetWithSkillsAsync(candidateId, ct);
        if (candidate is null) return;

        var cohort = await _cohorts.GetByIdAsync(candidate.CohortId, ct);
        if (cohort is null) return;

        var (folderId, webUrl) = await _folderProvisioner.EnsureCandidateFolderAsync(cohort.Name, candidate.AppUser.DisplayName, ct);
        candidate.SharePointFolderId = folderId;
        candidate.SharePointFolderWebUrl = webUrl;
        await _candidates.SaveChangesAsync(ct);
    }

    private async Task RunProjectAsync(int projectId, CancellationToken ct)
    {
        var project = await _projects.GetWithSponsorAsync(projectId, ct);
        if (project is null) return;

        var cohort = await _cohorts.GetByIdAsync(project.CohortId, ct);
        if (cohort is null) return;

        var (folderId, webUrl) = await _folderProvisioner.EnsureProjectFolderAsync(cohort.Name, project.Name, ct);
        project.SharePointFolderId = folderId;
        project.SharePointFolderWebUrl = webUrl;
        await _projects.SaveChangesAsync(ct);
    }
}
