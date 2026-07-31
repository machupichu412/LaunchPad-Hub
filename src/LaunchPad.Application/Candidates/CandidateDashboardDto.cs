using LaunchPad.Application.Assignments;

namespace LaunchPad.Application.Candidates;

/// <summary>
/// "Conversion readiness" (skill growth / sponsor feedback / deliverable quality)
/// is deliberately omitted — there's no historical tracking to compute those
/// defensibly (see plan). Only stats traceable to real data are included.
/// </summary>
public class CandidateDashboardDto
{
    public MyAssignmentDto? ActiveProject { get; set; }
    public int TasksComplete { get; set; }
    public int TasksTotal { get; set; }
    public decimal? MatchScore { get; set; }
    public int CommunityPostsThisWeek { get; set; }
}
