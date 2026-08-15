using System.Text.Json.Serialization;
using LaunchPad.Application.Risk;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Candidates;

/// <summary>
/// AverageScore and the risk flags are additive fields, populated only for
/// Executive/ProgramOps roles by CandidateDtoMapper. JsonIgnore(WhenWritingNull) means
/// an unauthorized role gets no score *key* in the payload, not just a null value —
/// see CLAUDE.md "hidden ratings" section, which requires the field to be absent.
/// </summary>
public class CandidateDto
{
    public int CandidateId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Location { get; set; }
    public Availability Availability { get; set; }
    public DateOnly? GraduationDate { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? Bio { get; set; }
    public string? School { get; set; }
    public string? Degree { get; set; }
    public decimal? Gpa { get; set; }
    public string[] Skills { get; set; } = Array.Empty<string>();
    public CandidateStatus Status { get; set; }
    public string Outcome { get; set; } = string.Empty;

    /// <summary>"Open in SharePoint" deep link — null until FolderProvisioningRunner sets it
    /// (or the self-heal path in AssignmentsController.SubmitDeliverable does). Not part of
    /// the hidden-ratings redaction — every role that can see a candidate at all sees this,
    /// same as Outcome.</summary>
    public string? SharePointFolderWebUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AverageScore { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasPerformanceRisk { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasEngagementRisk { get; set; }

    /// <summary>A recommendation, not a decision — see HireOutcomeRule and
    /// CandidatesController.UpdateStatus, which Ops uses to apply or override it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SuggestedHireOutcome? SuggestedHireOutcome { get; set; }
}
