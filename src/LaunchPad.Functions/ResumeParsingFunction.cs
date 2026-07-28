using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Fires on resume upload to Blob Storage. Calls Azure OpenAI for structured
/// extraction — latency is unpredictable, which is why this is async rather than
/// inline on the upload request. Phase 5 work; left as a stub until then.
/// </summary>
public sealed class ResumeParsingFunction
{
    private readonly ILogger<ResumeParsingFunction> _logger;

    public ResumeParsingFunction(ILogger<ResumeParsingFunction> logger) => _logger = logger;

    [Function(nameof(ResumeParsingFunction))]
    public Task RunAsync(
        [BlobTrigger("resumes/{name}", Connection = "BlobStorageConnection")] Stream resumeStream,
        string name)
    {
        _logger.LogInformation("Resume uploaded for parsing: {BlobName}", name);
        // TODO (Phase 5): call Azure OpenAI for structured extraction, write
        // CandidateSkill rows with Source = ResumeParsed.
        return Task.CompletedTask;
    }
}
