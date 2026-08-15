using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

public class Cohort
{
    public int CohortId { get; set; }
    public int ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CohortStatus Status { get; set; }

    /// <summary>Opaque Graph drive-item ID of this cohort's SharePoint folder
    /// ({parent}/{Name}) — null until FolderProvisioningRunner sets it.</summary>
    public string? SharePointFolderId { get; set; }

    /// <summary>Human-facing "open in SharePoint" deep link — captured once at provisioning time.</summary>
    public string? SharePointFolderWebUrl { get; set; }

    public Program Program { get; set; } = null!;
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
