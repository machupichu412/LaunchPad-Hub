using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Assignments;

public enum SponsorDirectRequestOutcome
{
    Created,
    CandidateNoLongerEligible,
    ProjectFull,
}

public sealed record SponsorDirectRequestResult(SponsorDirectRequestOutcome Outcome, Assignment? Assignment = null);

public enum OpsApproveOutcome
{
    NotFound,
    WrongStatus,
    CandidateConflict,
    ProjectFull,
    Approved,
}

public sealed record OpsApproveResult(OpsApproveOutcome Outcome, Assignment? Assignment = null);
