import type {
  Availability,
  AssignmentStatus,
  CandidateStatus,
  ProjectApprovalStatus,
  ProjectDeliveryStage,
  ProjectStatus,
  SuggestedHireOutcome,
} from '../api/types';

// These enums serialize as their bare C# member name (see appsettings' JsonStringEnumConverter),
// so multi-word values like "InProgress"/"PendingOps" render with no space if used directly —
// always go through these labels rather than interpolating the raw string.

const projectStatusLabels: Record<ProjectStatus, string> = {
  Open: 'Open',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
};

export function projectStatusLabel(status: ProjectStatus): string {
  return projectStatusLabels[status];
}

const projectApprovalStatusLabels: Record<ProjectApprovalStatus, string> = {
  Draft: 'Draft',
  PendingOps: 'Pending Review',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export function projectApprovalStatusLabel(status: ProjectApprovalStatus): string {
  return projectApprovalStatusLabels[status];
}

const availabilityLabels: Record<Availability, string> = {
  PartTime: 'Part-time',
  FullTime: 'Full-time',
};

export function availabilityLabel(availability: Availability): string {
  return availabilityLabels[availability];
}

const assignmentStatusLabels: Record<AssignmentStatus, string> = {
  Proposed: 'Proposed',
  SponsorApproved: 'Pending Ops Review',
  OpsApproved: 'Approved',
  Active: 'Active',
  Completed: 'Completed',
  Withdrawn: 'Withdrawn',
};

export function assignmentStatusLabel(status: AssignmentStatus): string {
  return assignmentStatusLabels[status];
}

const candidateStatusLabels: Record<CandidateStatus, string> = {
  InProgress: 'In Progress',
  Hire: 'Hire',
  TalentPlus: 'Talent Plus',
  NoHire: 'No Hire',
};

export function candidateStatusLabel(status: CandidateStatus): string {
  return candidateStatusLabels[status];
}

const suggestedHireOutcomeLabels: Record<SuggestedHireOutcome, string> = {
  NoHire: 'No Hire',
  Hire: 'Hire',
  TalentPlus: 'Talent Plus',
};

export function suggestedHireOutcomeLabel(outcome: SuggestedHireOutcome): string {
  return suggestedHireOutcomeLabels[outcome];
}

// Order matters here — it's the forward progression a Sponsor is restricted to
// (see ProjectsController.AdvanceDeliveryStage) and drives the stage picker's option order.
export const deliveryStageOrder: ProjectDeliveryStage[] = [
  'NotStarted',
  'MvpBuilt',
  'Showcased',
  'PilotReady',
  'BusinessValueDocumented',
];

const deliveryStageLabels: Record<ProjectDeliveryStage, string> = {
  NotStarted: 'Not started',
  MvpBuilt: 'MVP built',
  Showcased: 'Showcased',
  PilotReady: 'Pilot-ready',
  BusinessValueDocumented: 'Business value documented & signed off',
};

export function deliveryStageLabel(stage: ProjectDeliveryStage): string {
  return deliveryStageLabels[stage];
}
