using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// The uploaded file lives in the candidate's SharePoint folder, addressed by the opaque
/// SharePointItemId (via IDocumentStorage) — never a Blob path, and never a direct SharePoint
/// URL the client is expected to fetch content from itself; every read is proxied through
/// AssignmentsController's download endpoint.
/// </summary>
public class Deliverable
{
    public int DeliverableId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DeliverableStatus Status { get; set; }
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Opaque Graph drive-item ID of the uploaded file — set by IDocumentStorage.SaveAsync.</summary>
    public string? SharePointItemId { get; set; }

    /// <summary>Optional — a candidate may attach a deliverable to the specific to-do it
    /// completes, or leave it unattached. Must belong to the same Assignment (checked in
    /// AssignmentsController, not here — this entity has no query access to enforce it).</summary>
    public int? ProjectTodoId { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public ProjectTodo? ProjectTodo { get; set; }
}
