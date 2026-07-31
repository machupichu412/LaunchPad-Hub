using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Cohorts;

public class CohortDto
{
    public int CohortId { get; set; }
    public int ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CohortStatus Status { get; set; }
    public int CandidateCount { get; set; }
    public int ProjectCount { get; set; }
}
