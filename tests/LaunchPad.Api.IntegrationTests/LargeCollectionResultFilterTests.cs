using FluentAssertions;
using LaunchPad.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// The tripwire that tells us which unpaged endpoints have outgrown a single response is
/// only useful if it actually fires, and only tolerable if it stays quiet the rest of the
/// time — a warning on every list response would train everyone to ignore it.
/// </summary>
public class LargeCollectionResultFilterTests
{
    [Fact]
    public void ACollectionOverTheThresholdWarnsAndNamesTheRoute()
    {
        var logger = new CapturingLogger();
        var rows = Enumerable.Range(0, LargeCollectionResultFilter.WarnThreshold + 1).ToList();

        new LargeCollectionResultFilter(logger).OnResultExecuting(ContextFor(rows, "api/candidates"));

        logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("api/candidates").And.Contain(rows.Count.ToString());
    }

    [Fact]
    public void ACollectionAtTheThresholdStaysQuiet()
    {
        var logger = new CapturingLogger();
        var rows = Enumerable.Range(0, LargeCollectionResultFilter.WarnThreshold).ToList();

        new LargeCollectionResultFilter(logger).OnResultExecuting(ContextFor(rows, "api/candidates"));

        logger.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void AResultThatIsNotACollectionIsIgnored()
    {
        var logger = new CapturingLogger();

        // A single DTO has no Count to compare — the filter must not assume every ObjectResult
        // is a list, since most write endpoints return exactly this shape.
        new LargeCollectionResultFilter(logger).OnResultExecuting(ContextFor(new { CandidateId = 1 }, "api/candidates/1"));

        logger.Warnings.Should().BeEmpty();
    }

    private static ResultExecutingContext ContextFor(object value, string route) =>
        new(
            new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new ActionDescriptor { AttributeRouteInfo = new() { Template = route } }),
            [],
            new ObjectResult(value),
            controller: null!);

    private sealed class CapturingLogger : ILogger<LargeCollectionResultFilter>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
