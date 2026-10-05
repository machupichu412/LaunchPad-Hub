using System.Globalization;
using System.Threading.RateLimiting;
using LaunchPad.Api.Authorization;
using Microsoft.Identity.Web;

namespace LaunchPad.Api.Middleware;

public static class ApplicationRateLimits
{
    public static int Positive(IConfiguration configuration, string key, int fallback)
    {
        var value = configuration.GetValue<int?>(key) ?? fallback;
        return value > 0 ? value : throw new InvalidOperationException($"{key} must be positive.");
    }

    private static string UserKey(HttpContext context) => ApplicationIdentity.IsValid(context.User)
        ? context.User.GetObjectId()! : "anonymous";

    private static PartitionedRateLimiter<HttpContext> Window(
        Func<HttpContext, string> key, int permits, TimeSpan window, Func<HttpContext, bool>? applies = null) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            applies is not null && !applies(context)
                ? RateLimitPartition.GetNoLimiter<string>("exempt")
                : RateLimitPartition.GetSlidingWindowLimiter(key(context), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = window,
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

    public static void AddApplicationRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var permits = Positive(configuration, "RateLimiting:PermitLimit", 300);
        var seconds = Positive(configuration, "RateLimiting:WindowSeconds", 60);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                Window(UserKey, permits, TimeSpan.FromSeconds(seconds)),
                Window(ctx => UserKey(ctx) + ":write", Positive(configuration, "RateLimiting:WritePermitLimit", 30), TimeSpan.FromMinutes(1),
                    ctx => !(HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method))),
                Window(UserKey, Positive(configuration, "RateLimiting:UploadsPerHour", 6), TimeSpan.FromHours(1),
                    ctx => ApplicationIdentity.IsValid(ctx.User) && IsUpload(ctx)),
                Window(_ => "instance-uploads", Positive(configuration, "RateLimiting:InstanceUploadsPerHour", 60), TimeSpan.FromHours(1),
                    ctx => ApplicationIdentity.IsValid(ctx.User) && IsUpload(ctx)),
                Window(_ => "instance-upload-day", Positive(configuration, "RateLimiting:InstanceUploadsPerDay", 100), TimeSpan.FromDays(1),
                    ctx => ApplicationIdentity.IsValid(ctx.User) && IsUpload(ctx)),
                Window(_ => "instance-download-day", Positive(configuration, "RateLimiting:InstanceDownloadsPerDay", 500), TimeSpan.FromDays(1),
                    ctx => ApplicationIdentity.IsValid(ctx.User) && HasPolicy(ctx, RateLimitPolicies.Download)),
                Window(_ => "instance-media-day", Positive(configuration, "RateLimiting:InstanceMediaReadsPerDay", 10000), TimeSpan.FromDays(1),
                    ctx => ApplicationIdentity.IsValid(ctx.User) && HasPolicy(ctx, RateLimitPolicies.MediaRead)),
                Window(_ => "instance-matching", 10, TimeSpan.FromHours(1), ctx => ApplicationIdentity.IsValid(ctx.User) && ctx.User.IsInRole(LaunchPad.Application.Common.Roles.ProgramOps) && HasPolicy(ctx, RateLimitPolicies.Matching)),
                PartitionedRateLimiter.Create<HttpContext, string>(ctx => ApplicationIdentity.IsValid(ctx.User) && IsUpload(ctx)
                    ? RateLimitPartition.GetConcurrencyLimiter("uploads", _ => new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter<string>("exempt")));
            options.OnRejected = (context, _) =>
            {
                SetRetryAfter(context.HttpContext, context.Lease);
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(RateLimitPolicies.Readiness, _ =>
                RateLimitPartition.GetFixedWindowLimiter("readiness", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy(RateLimitPolicies.CommunityWrite, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(UserKey(ctx), _ => new SlidingWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 10,
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
            // Marker for the chained global upload quotas; community multipart posts share them.
            options.AddPolicy(RateLimitPolicies.Upload, _ => RateLimitPartition.GetNoLimiter<string>("upload-marker"));
            options.AddPolicy(RateLimitPolicies.MediaRead, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(UserKey(ctx), _ => new SlidingWindowRateLimiterOptions
                { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
            options.AddPolicy(RateLimitPolicies.Download, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(UserKey(ctx), _ => new SlidingWindowRateLimiterOptions
                { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
            // Shared across Ops users; the global limiter also caps total matching jobs.
            options.AddPolicy(RateLimitPolicies.Matching, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ApplicationIdentity.IsValid(ctx.User) && ctx.User.IsInRole(LaunchPad.Application.Common.Roles.ProgramOps)
                        && int.TryParse(ctx.Request.Query["cohortId"], out var id) ? $"cohort:{id}" : "invalid-cohort",
                    _ => new SlidingWindowRateLimiterOptions
                    { PermitLimit = 1, Window = TimeSpan.FromMinutes(5), SegmentsPerWindow = 6, QueueLimit = 0 }));
        });
    }

    private static bool IsUpload(HttpContext context) =>
        HasPolicy(context, RateLimitPolicies.Upload)
        || context.GetEndpoint()?.Metadata.GetMetadata<UploadWorkloadAttribute>() is not null;

    private static bool HasPolicy(HttpContext context, string policy) =>
        context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName == policy;

    public static void SetRetryAfter(HttpContext context, RateLimitLease lease)
    {
        var seconds = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 1;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers.CacheControl = "no-store";
    }
}
