namespace LaunchPad.Application.Cohorts;

// No ProgramId — this demo only ever seeds one Program, so the repository
// resolves it automatically (same demo-scoped simplification as the
// hardcoded DEMO_COHORT_ID used elsewhere until real cohort/program
// selection exists).
public class CreateCohortRequest
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
