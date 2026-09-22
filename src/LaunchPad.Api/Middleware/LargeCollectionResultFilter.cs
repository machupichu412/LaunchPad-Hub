using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LaunchPad.Api.Middleware;

/// <summary>
/// Logs a warning when a list endpoint returns more rows than anyone designed for.
///
/// ~25 actions across 11 controllers return uncapped collections. Retrofitting pagination
/// onto all of them is a coordinated backend + frontend contract change, and the dataset is
/// bounded by the business for now (~2,000 named users total, ever), so the honest move is to
/// find out which endpoints actually grow before paying for that migration. This is the
/// tripwire: one filter, no signature changes, no frontend work. When a warning starts firing
/// in production, that endpoint — and only that endpoint — has earned pagination.
///
/// ponytail: logging only, no cap. Truncating a response silently would hide data rather than
/// the latency; paginate the endpoints this names instead of raising the threshold.
/// </summary>
public sealed class LargeCollectionResultFilter : IResultFilter
{
    /// <summary>Roughly one full cohort. Below this, a single unpaged response is fine.</summary>
    public const int WarnThreshold = 250;

    private readonly ILogger<LargeCollectionResultFilter> _logger;

    public LargeCollectionResultFilter(ILogger<LargeCollectionResultFilter> logger) => _logger = logger;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: ICollection collection }) return;
        if (collection.Count <= WarnThreshold) return;

        // The route template, not the request path — the path carries ids (and for /me-style
        // routes, effectively a user identifier), and this line is only useful aggregated by
        // endpoint anyway.
        _logger.LogWarning(
            "Unpaged response returned {RowCount} rows from {Route} (threshold {Threshold}) — this endpoint has outgrown a single response.",
            collection.Count,
            context.ActionDescriptor.AttributeRouteInfo?.Template ?? context.ActionDescriptor.DisplayName,
            WarnThreshold);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
