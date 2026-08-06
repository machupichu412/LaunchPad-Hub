using LaunchPad.Domain.Common;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// A candidate's own interest rating (1-5) for one project on the marketplace. One row
/// per (Candidate, Project) — rating again updates the existing row rather than adding
/// a new one. Feeds as a small nudge into the matching engine's score, never a gate.
/// </summary>
public class ProjectInterest : Entity
{
    public int ProjectInterestId { get; set; }
    public int ProjectId { get; set; }
    public int CandidateId { get; set; }
    public byte Rating { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public Candidate Candidate { get; set; } = null!;
}
