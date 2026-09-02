using LaunchPad.Domain.Common;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

public class Project : Entity
{
    public int ProjectId { get; set; }
    public int CohortId { get; set; }
    public int SponsorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Availability AvailabilityNeeded { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectApprovalStatus ApprovalStatus { get; set; }
    public ProjectStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public int MaxCandidates { get; set; } = 1;

    /// <summary>Delivery-stage milestone backing the Executive KPI dashboard — see
    /// ProjectDeliveryStage and ProjectsController.AdvanceDeliveryStage.</summary>
    public ProjectDeliveryStage DeliveryStage { get; set; } = ProjectDeliveryStage.NotStarted;

    /// <summary>Opaque Graph drive-item ID of this project's SharePoint folder
    /// ({parent}/{Cohort}/Projects/{Name}) — null until FolderProvisioningRunner sets it.</summary>
    public string? SharePointFolderId { get; set; }

    /// <summary>Human-facing "open in SharePoint" deep link — the one deliberate exception
    /// to the opaque-key rule, captured once at provisioning time.</summary>
    public string? SharePointFolderWebUrl { get; set; }

    public Cohort Cohort { get; set; } = null!;
    public Sponsor Sponsor { get; set; } = null!;
    public ICollection<ProjectSkill> Skills { get; set; } = new List<ProjectSkill>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<ProjectInterest> Interests { get; set; } = new List<ProjectInterest>();
}
