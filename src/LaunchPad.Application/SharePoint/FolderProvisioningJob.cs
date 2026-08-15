namespace LaunchPad.Application.SharePoint;

public enum FolderProvisioningTargetType
{
    Cohort,
    Candidate,
    Project,
}

/// <summary>One message shape for all three "provision X folder" cases — why one queue and
/// one Function are enough (see SharePointProvisioningFunction).</summary>
public sealed record FolderProvisioningJob(FolderProvisioningTargetType TargetType, int TargetId);
