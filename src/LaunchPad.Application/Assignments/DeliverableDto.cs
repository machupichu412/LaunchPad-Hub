using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public class DeliverableDto
{
    public int DeliverableId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DeliverableStatus Status { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public int? ProjectTodoId { get; set; }
    public string? ProjectTodoTitle { get; set; }

    /// <summary>Whether a file was actually uploaded to SharePoint for this deliverable —
    /// gates showing a download button. The opaque SharePointItemId itself never reaches
    /// the client; content is only ever reached through the download-proxy endpoint.</summary>
    public bool HasFile { get; set; }
}

/// <summary>
/// The metadata half of a multipart deliverable upload — deliberately framework-agnostic
/// (no IFormFile/Stream field here). LaunchPad.Application is a plain class library with no
/// ASP.NET Core hosting reference (see CLAUDE.md's layering rule), so the controller extracts
/// these primitives from the bound IFormFile and validates this shape; the actual file stream
/// is passed straight from the controller to IDocumentStorage.SaveAsync without ever
/// round-tripping through this DTO.
/// </summary>
public class SubmitDeliverableRequest
{
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; }

    /// <summary>Optional — attaches this deliverable to a specific to-do on the same
    /// assignment. AssignmentsController checks the todo actually belongs to this
    /// assignment (a FluentValidation rule can't do that DB lookup).</summary>
    public int? ProjectTodoId { get; set; }
}
