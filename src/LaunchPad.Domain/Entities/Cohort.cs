using LaunchPad.Domain.Enums;

namespace LaunchPad.Domain.Entities;

public class Cohort
{
    public int CohortId { get; set; }
    public int ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CohortStatus Status { get; set; }

    public Program Program { get; set; } = null!;
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
