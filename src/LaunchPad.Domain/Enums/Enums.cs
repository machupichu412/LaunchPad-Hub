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
