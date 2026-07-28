namespace LaunchPad.Domain.Entities;

public class Program
{
    public int ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Cohort> Cohorts { get; set; } = new List<Cohort>();
}
