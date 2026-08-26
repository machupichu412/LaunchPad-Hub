using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

/// <summary>
/// Backs the engagement-risk signal in vCandidateRisk (stale/overdue to-dos,
/// last-completion recency). Not itself a source of truth for assignment state.
/// </summary>
public class ProjectTodo
{
    public int ProjectTodoId { get; set; }
    public int AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TodoStatus Status { get; set; }
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedUtc { get; set; }

    /// <summary>Set together (or both null). When set, this to-do represents an
    /// Ops-scheduled review obligation rather than an ordinary sponsor-created task —
    /// see CohortsController.ScheduleReviews. Completion happens automatically when the
    /// matching Review is submitted (ReviewsController.Submit), never a manual status
    /// click. Who acts on it is derived, not stored: SponsorOnCandidate is the Sponsor's
    /// to act on, CandidateOnSponsor/ProjectEval (or null, the ordinary case) are the
    /// Candidate's.</summary>
    public ReviewType? LinkedReviewType { get; set; }
    public Checkpoint? LinkedReviewCheckpoint { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
