using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// Metadata-only for now — no Blob Storage locally, same deferred-infra scoping as
/// resume upload. Title/FileName/Status/SubmittedUtc are real; there's no backing file.
/// </summary>
public class Deliverable
{
    public int DeliverableId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DeliverableStatus Status { get; set; }
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;

    public Assignment Assignment { get; set; } = null!;
}
