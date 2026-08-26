namespace LaunchPad.Api;

/// <summary>Named rate-limiter policy identifiers, referenced by both Program.cs's
/// AddRateLimiter registration and the controller actions' [EnableRateLimiting] attributes.</summary>
public static class RateLimitPolicies
{
    /// <summary>Community post/comment creation — a lightweight per-user spam/runaway-script
    /// guard, not applied to reads or reactions. See Program.cs.</summary>
    public const string CommunityWrite = nameof(CommunityWrite);
}
