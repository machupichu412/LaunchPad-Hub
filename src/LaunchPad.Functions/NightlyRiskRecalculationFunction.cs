using LaunchPad.Application.Common;
using LaunchPad.Application.Notifications;
using LaunchPad.Application.Reporting;
using LaunchPad.Application.Risk;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LaunchPad.Functions;

/// <summary>
/// Off-peak, full-table scan. Risk itself is always computed by dbo.vCandidateRisk
/// (via IOpsDashboardRepository.GetAtRiskCandidatesAsync — never re-derived here, per
/// CLAUDE.md's "risk signals are a computed view" rule); this function's job is
/// reacting to it: newly-flagged candidates get an AuditEvent row and a Program Ops
/// notification (launchpad-build-guide.md §5.4's "sponsor auto-flag"). "Newly" is
/// tracked via the audit trail itself (see SponsorAutoFlagRule), so a candidate isn't
/// re-notified every night the flag stays true, and can be re-flagged later if they
/// recover and relapse. Risk flags are Executive/ProgramOps-only per the redaction
/// rule in CLAUDE.md, so the notification goes to Program Ops, never the sponsor.
/// </summary>
public sealed class NightlyRiskRecalculationFunction
{
    private const string EntityName = "Candidate";
    private const string FlaggedAction = "AutoFlagged";
    private const string ClearedAction = "AutoFlagCleared";

    private readonly IOpsDashboardRepository _opsDashboard;
    private readonly IAuditLog _auditLog;
    private readonly INotificationPublisher _notifications;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NightlyRiskRecalculationFunction> _logger;

    public NightlyRiskRecalculationFunction(
        IOpsDashboardRepository opsDashboard,
        IAuditLog auditLog,
        INotificationPublisher notifications,
        IConfiguration configuration,
        ILogger<NightlyRiskRecalculationFunction> logger)
    {
        _opsDashboard = opsDashboard;
        _auditLog = auditLog;
        _notifications = notifications;
        _configuration = configuration;
        _logger = logger;
    }

    [Function(nameof(NightlyRiskRecalculationFunction))]
    public async Task RunAsync([TimerTrigger("0 0 3 * * *")] TimerInfo timer, CancellationToken ct)
    {
        _logger.LogInformation("Nightly risk recalculation run started at: {Time}", DateTime.UtcNow);

        var atRisk = await _opsDashboard.GetAtRiskCandidatesAsync(take: null, ct);
        var currentlyAtRiskIds = atRisk.Select(c => c.CandidateId).ToHashSet();
        var alreadyFlaggedIds = await _auditLog.GetEntityIdsWithLatestActionAsync(EntityName, FlaggedAction, ct);

        var snapshots = atRisk.Select(c => new CandidateRiskSnapshot(c.CandidateId, c.HasPerformanceRisk, c.HasEngagementRisk));
        var newlyFlagged = SponsorAutoFlagRule.EvaluateNewlyFlagged(snapshots, alreadyFlaggedIds);
        var recovered = SponsorAutoFlagRule.EvaluateRecovered(currentlyAtRiskIds, alreadyFlaggedIds);

        var programOpsUpn = _configuration["Notifications:ProgramOpsUpn"];

        foreach (var candidate in newlyFlagged)
        {
            var detail = atRisk.First(c => c.CandidateId == candidate.CandidateId);
            var riskKind = candidate.HasPerformanceRisk && candidate.HasEngagementRisk
                ? "performance and engagement"
                : candidate.HasPerformanceRisk ? "performance" : "engagement";

            await _auditLog.RecordAsync(
                Guid.Empty, // system-triggered — no human actor behind a scheduled job
                EntityName,
                candidate.CandidateId.ToString(),
                FlaggedAction,
                reason: $"Nightly risk recalculation: {riskKind} risk crossed threshold.",
                ct: ct);

            if (!string.IsNullOrWhiteSpace(programOpsUpn))
            {
                await _notifications.PublishAsync(new NotificationMessage(
                    programOpsUpn,
                    $"Candidate at risk: {detail.DisplayName}",
                    $"{detail.DisplayName} ({detail.CohortName}) has newly crossed the {riskKind} risk threshold. " +
                    "Visit the Risks dashboard for details."), ct);
            }
        }

        foreach (var candidateId in recovered)
        {
            await _auditLog.RecordAsync(Guid.Empty, EntityName, candidateId.ToString(), ClearedAction, ct: ct);
        }

        _logger.LogInformation(
            "Nightly risk recalculation completed: {AtRiskCount} at risk, {NewlyFlaggedCount} newly flagged, {RecoveredCount} recovered.",
            atRisk.Count, newlyFlagged.Count, recovered.Count);
    }
}
