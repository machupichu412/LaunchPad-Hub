using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public class DeliverableDto
{
    public int DeliverableId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DeliverableStatus Status { get; set; }
    public DateTime SubmittedUtc { get; set; }
}

// Metadata-only — no Blob Storage locally (see plan). Title/FileName are recorded for
// real; there's no backing file until real Blob SAS upload is built later.
public class CreateDeliverableRequest
{
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
