namespace LaunchPad.Api;

public static class RateLimitPolicies
{
    public const string CommunityWrite = nameof(CommunityWrite);
    public const string Upload = nameof(Upload);
    public const string MediaRead = nameof(MediaRead);
    public const string Download = nameof(Download);
    public const string Readiness = nameof(Readiness);
    public const string Matching = nameof(Matching);
}

/// <summary>Multipart endpoints that share storage quotas while retaining another endpoint policy.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UploadWorkloadAttribute : Attribute { }
