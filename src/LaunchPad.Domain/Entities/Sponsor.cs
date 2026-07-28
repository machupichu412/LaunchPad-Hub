namespace LaunchPad.Domain.Entities;

public class Sponsor
{
    public int SponsorId { get; set; }
    public int AppUserId { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RemovalReason { get; set; }

    public AppUser AppUser { get; set; } = null!;
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
