namespace LaunchPad.Domain.Entities;

/// <summary>
/// Identity anchor is EntraObjectId. Rows are JIT-provisioned on first authenticated
/// request (upsert on EntraObjectId) — never a locally-managed password or user list.
/// </summary>
public class AppUser
{
    public int AppUserId { get; set; }
    public Guid EntraObjectId { get; set; }
    public string Upn { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
