namespace LaunchPad.Domain.Entities;

public class ProjectExpert
{
    public int ProjectId { get; set; }
    public int AppUserId { get; set; }
    public Project Project { get; set; } = null!;
    public AppUser AppUser { get; set; } = null!;
}

public class ProjectQuestion
{
    public int ProjectQuestionId { get; set; }
    public int ProjectId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    // A technical deliverable review is a discussion, not a hidden candidate rating.
    public int? DeliverableId { get; set; }
    public Project Project { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
    public Deliverable? Deliverable { get; set; }
    public ICollection<ProjectAnswer> Answers { get; set; } = new List<ProjectAnswer>();
}

public class ProjectAnswer
{
    public int ProjectAnswerId { get; set; }
    public int ProjectQuestionId { get; set; }
    public int AuthorAppUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string? FileName { get; set; }
    public string? FileItemId { get; set; }
    public ProjectQuestion Question { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
}
