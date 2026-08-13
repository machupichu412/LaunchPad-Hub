namespace LaunchPad.Application.Search;

public class SearchResultDto
{
    /// <summary>"Project" or "Candidate" — the frontend groups the dropdown by this.</summary>
    public string Type { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Computed server-side per the caller's role, since route access is
    /// asymmetric across roles (e.g. only ProgramOps can reach /ops/projects/{id}) —
    /// always points somewhere the caller can actually land, never a 403.</summary>
    public string Url { get; set; } = string.Empty;
}
