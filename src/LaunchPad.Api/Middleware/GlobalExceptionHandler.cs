using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LaunchPad.Api.Middleware;

/// <summary>
/// Turns any unhandled exception into a ProblemDetails response instead of the bare
/// Kestrel 500 that used to reach clients. The correlation id goes in the body as well
/// as the header so a user can quote it from a screenshot — it is the only link between
/// what they saw and the logged exception, which stays server-side.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
            ? value?.ToString()
            : null;

        var sanitizedMethod = SanitizeForLog(context.Request.Method);
        var sanitizedPath = SanitizeForLog(context.Request.Path.ToString());

        _logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            sanitizedMethod,
            sanitizedPath);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            // Deliberately not exception.Message — it can carry connection strings, SQL
            // fragments, or candidate data. The correlation id is what a user reports.
            Detail = "The request could not be completed. Quote the correlation id when reporting this.",
            Instance = context.Request.Path,
        };

        if (correlationId is not null)
        {
            problem.Extensions["correlationId"] = correlationId;
            // UseExceptionHandler clears the response before handing it to us, which drops
            // the header CorrelationIdMiddleware already set. Put it back, or a 500 — the
            // one response where the id matters most — would be the only one without it.
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}
