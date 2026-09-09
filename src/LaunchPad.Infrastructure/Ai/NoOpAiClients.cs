using LaunchPad.Application.Ai;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Infrastructure.Ai;

/// <summary>
/// Selected in every host when AzureOpenAI:Endpoint is blank — the same "gracefully degrade
/// for local dev" shape already used for SharePoint:SiteId and Storage:AccountUrl. This is what
/// lets scripts/run-local-demo.sh exercise the whole resume and matching pipeline end to end
/// with no Azure account at all.
///
/// Each of these returns the interface's documented "unavailable" value rather than throwing.
/// Every caller already has to handle that path for a real outage, so exercising it locally is
/// a feature: the degraded path is the one most likely to rot unnoticed otherwise.
/// </summary>
public sealed class NoOpResumeExtractionClient : IResumeExtractionClient
{
    private readonly ILogger<NoOpResumeExtractionClient> _logger;

    public NoOpResumeExtractionClient(ILogger<NoOpResumeExtractionClient> logger) => _logger = logger;

    public Task<ResumeExtraction?> ExtractAsync(
        string resumeText, IReadOnlyCollection<string> knownSkillNames, CancellationToken ct = default)
    {
        // Length only — never the text itself. Resume text is the most PII-dense data in the
        // system and this line would otherwise be the easiest place to leak it into logs.
        _logger.LogInformation(
            "Resume extraction skipped: no Azure OpenAI endpoint configured ({CharacterCount} characters of resume text).",
            resumeText.Length);
        return Task.FromResult<ResumeExtraction?>(null);
    }
}

public sealed class NoOpEmbeddingClient : IEmbeddingClient
{
    private readonly ILogger<NoOpEmbeddingClient> _logger;

    public NoOpEmbeddingClient(ILogger<NoOpEmbeddingClient> logger) => _logger = logger;

    /// <summary>Named so a vector never silently escapes into a cache row keyed as if it came
    /// from a real model.</summary>
    public string ModelName => "none";

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Embedding skipped: no Azure OpenAI endpoint configured ({TextCount} texts).", texts.Count);
        return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());
    }
}

public sealed class NoOpMatchRationaleWriter : IMatchRationaleWriter
{
    private readonly ILogger<NoOpMatchRationaleWriter> _logger;

    public NoOpMatchRationaleWriter(ILogger<NoOpMatchRationaleWriter> logger) => _logger = logger;

    public Task<string?> WriteAsync(MatchRationaleContext context, CancellationToken ct = default)
    {
        _logger.LogDebug("Match rationale generation skipped: no Azure OpenAI endpoint configured.");
        return Task.FromResult<string?>(null);
    }
}
