using System.Threading.RateLimiting;

namespace LaunchPad.Api.Middleware;

/// <summary>Process-wide ceilings bound work even when identities/IPs rotate. No waiting queue.</summary>
public sealed class RequestAdmissionMiddleware : IMiddleware, IDisposable
{
    private readonly RateLimiter _requests;
    private readonly RateLimiter _concurrency;

    public RequestAdmissionMiddleware(IConfiguration configuration)
    {
        _requests = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = ApplicationRateLimits.Positive(configuration, "RateLimiting:InstancePermitLimit", 3000),
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0
        });
        _concurrency = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = ApplicationRateLimits.Positive(configuration, "RateLimiting:ConcurrentRequests", 100),
            QueueLimit = 0
        });
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Liveness is intentionally cheap and does not query dependencies or provision users.
        if (context.Request.Path == "/healthz" && HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        using var requestLease = _requests.AttemptAcquire();
        using var concurrencyLease = requestLease.IsAcquired ? _concurrency.AttemptAcquire() : null;
        if (!requestLease.IsAcquired || concurrencyLease?.IsAcquired != true)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ApplicationRateLimits.SetRetryAfter(context, requestLease);
            return;
        }

        await next(context);
    }

    public void Dispose()
    {
        _requests.Dispose();
        _concurrency.Dispose();
    }
}
