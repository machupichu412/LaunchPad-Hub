namespace LaunchPad.Domain.Enums;

public enum CohortStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2
}

public enum Availability
{
    PartTime = 0,
    FullTime = 1
}

public enum CandidateStatus
{
    InProgress = 0,
    Hire = 1,
    TalentPlus = 2,
    NoHire = 3
}

public enum ProjectApprovalStatus
{
    Draft = 0,
    PendingOps = 1,
    Approved = 2,
    Rejected = 3
}

public enum ProjectStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum SkillSource
{
    SelfReported = 0,
    ResumeParsed = 1,
    OpsVerified = 2
}

public enum AssignmentStatus
{
    Proposed = 0,
    SponsorApproved = 1,
    OpsApproved = 2,
    Active = 3,
    Completed = 4,
    Withdrawn = 5
}

public enum ReviewType
{
    SponsorOnCandidate = 0,
    CandidateOnSponsor = 1,
    ProjectEval = 2
}

public enum Checkpoint
{
    Midpoint = 0,
    Final = 1
}

public enum TodoStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

public enum TodoPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum DeliverableStatus
{
    Draft = 0,
    Submitted = 1
}

public enum CommunityPostType
{
    Win = 0,
    Question = 1,
    Announcement = 2,
    Kudos = 3,
    Reminder = 4
}

/// <summary>
/// A project's delivery-stage milestone — the single ordered progression backing the
/// "AI solutions delivered / business value / prototype maturity / pilot & adoption
/// readiness" Executive KPIs (see dbo.vProjectDeliveryKpi and ExecutiveDashboardDto).
/// Values are ordered on purpose: KPI thresholds are ">= stage" comparisons. A Sponsor
/// can only move their own project forward one story at a time; Program Ops can set
/// any value, including backward, to correct a mistake — see
/// ProjectsController.AdvanceDeliveryStage.
/// </summary>
public enum ProjectDeliveryStage
{
    NotStarted = 0,
    MvpBuilt = 1,
    Showcased = 2,
    PilotReady = 3,

    /// <summary>Terminal stage: business value documented AND the sponsor has signed off —
    /// the slide's "100% documented with sponsor sign-off" is one combined target, not two,
    /// so it's modeled as one stage rather than splitting documentation from sign-off.
    /// A project at this stage counts toward "AI solutions delivered."</summary>
    BusinessValueDocumented = 4
}
