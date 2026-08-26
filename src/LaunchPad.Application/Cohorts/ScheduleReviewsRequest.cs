using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Cohorts;

public class ScheduleReviewsRequest
{
    public Checkpoint Checkpoint { get; set; }
    public DateOnly DueDate { get; set; }
}

/// <summary>AssignmentsScheduled counts assignments that got at least one new to-do;
/// TodosCreated is the raw row count (up to 3 per assignment) — already-scheduled
/// assignment/type/checkpoint combinations are skipped, not duplicated.</summary>
public class ScheduleReviewsResult
{
    public int AssignmentsScheduled { get; set; }
    public int TodosCreated { get; set; }
}
