using System.Text.Json.Serialization;
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
    public string? Location { get; set; }
    public Availability Availability { get; set; }
    public DateOnly? GraduationDate { get; set; }
    public string[] Skills { get; set; } = Array.Empty<string>();
    public string Outcome { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AverageScore { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasPerformanceRisk { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasEngagementRisk { get; set; }
}
