namespace LaunchPad.Application.Matching;

/// <summary>Carries the acting Ops user through the queue — a Service-Bus-triggered
/// Function has no ICurrentUser/ambient request to attribute the audit event to.</summary>
public sealed record CohortMatchingJob(int CohortId, Guid TriggeredByEntraObjectId);
