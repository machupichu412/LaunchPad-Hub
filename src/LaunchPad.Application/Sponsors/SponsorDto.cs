namespace LaunchPad.Application.Sponsors;

public class SponsorDto
{
    public int SponsorId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string? Title { get; set; }
}
