// Hand-written placeholder types. Once the API's OpenAPI doc is published, replace
// this file with a generated client (nswag or openapi-typescript-codegen) so DTOs
// can't drift from the backend — see launchpad-build-guide.md §7.1.

export type Availability = 'PartTime' | 'FullTime';

export interface CandidateDto {
  candidateId: number;
  displayName: string;
  email: string | null;
  location: string | null;
  availability: Availability;
  graduationDate: string | null;
  linkedInUrl: string | null;
  portfolioUrl: string | null;
  bio: string | null;
  school: string | null;
  degree: string | null;
  gpa: number | null;
  skills: string[];
  outcome: string;
  averageScore?: number | null;
  hasPerformanceRisk?: boolean | null;
  hasEngagementRisk?: boolean | null;
}

export interface MeResponse {
  objectId: string | null;
  displayName: string | null;
  roles: string[];
}

export interface UpdateCandidateProfileRequest {
  location: string | null;
  availability: Availability;
  graduationDate: string | null;
  linkedInUrl: string | null;
  portfolioUrl: string | null;
  bio: string | null;
  school: string | null;
  degree: string | null;
  gpa: number | null;
  skillNames: string[];
}

export interface CreateCandidateProfileRequest {
  location: string | null;
  availability: Availability;
  graduationDate: string | null;
  linkedInUrl: string | null;
  portfolioUrl: string | null;
  bio: string | null;
  school: string | null;
  degree: string | null;
  gpa: number | null;
  skillIds: number[];
}

export interface SkillDto {
  skillId: number;
  name: string;
  skillCategoryId: number;
  skillCategoryName: string;
}

export interface SkillCategoryDto {
  skillCategoryId: number;
  name: string;
}

export interface CreateSkillRequest {
  name: string;
  skillCategoryId: number;
}

export type ProjectApprovalStatus = 'Draft' | 'PendingOps' | 'Approved' | 'Rejected';
export type ProjectStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export interface ProjectSkillDto {
  skillName: string;
  category: string;
  isRequired: boolean;
}

export interface ProjectDto {
  projectId: number;
  cohortId: number;
  sponsorId: number;
  sponsorName: string;
  name: string;
  description: string | null;
  availabilityNeeded: Availability;
  startDate: string | null;
  endDate: string | null;
  approvalStatus: ProjectApprovalStatus;
  status: ProjectStatus;
  rejectionReason: string | null;
  sponsorTeamsLink: string;
  myInterestRating: number | null;
  requiredSkills: ProjectSkillDto[];
}

export interface RateInterestRequest {
  rating: number;
}

export interface RejectProjectRequest {
  reason: string;
}

export interface CreateProjectRequest {
  cohortId: number;
  name: string;
  description: string | null;
  availabilityNeeded: Availability;
  startDate: string | null;
  endDate: string | null;
  requiredSkillNames: string[];
  preferredSkillNames: string[];
}

export type UpdateProjectRequest = Omit<CreateProjectRequest, 'cohortId'>;

export interface SponsorDto {
  sponsorId: number;
  displayName: string;
  organization: string | null;
  title: string | null;
}

export interface CreateSponsorProfileRequest {
  organization: string | null;
  title: string | null;
}

export type AssignmentStatus = 'Proposed' | 'SponsorApproved' | 'OpsApproved' | 'Active' | 'Completed' | 'Withdrawn';
export type TodoStatus = 'NotStarted' | 'InProgress' | 'Completed';
export type TodoPriority = 'Low' | 'Medium' | 'High';
export type DeliverableStatus = 'Draft' | 'Submitted';
export type Checkpoint = 'Midpoint' | 'Final';
export type CommunityPostType = 'Win' | 'Question' | 'Announcement' | 'Kudos' | 'Reminder';

export interface MyAssignmentDto {
  assignmentId: number;
  status: AssignmentStatus;
  startDate: string | null;
  endDate: string | null;
  matchScore: number | null;
  matchRationale: string | null;
  projectId: number;
  projectName: string;
  projectDescription: string | null;
  projectSkills: string[];
  sponsorName: string;
  sponsorOrganization: string | null;
  tasksTotal: number;
  tasksComplete: number;
}

export interface ProjectTodoDto {
  projectTodoId: number;
  title: string;
  status: TodoStatus;
  priority: TodoPriority;
  dueDate: string | null;
}

export interface UpdateTodoStatusRequest {
  status: TodoStatus;
}

export interface DeliverableDto {
  deliverableId: number;
  title: string;
  fileName: string;
  status: DeliverableStatus;
  submittedUtc: string;
}

export interface CreateDeliverableRequest {
  title: string;
  fileName: string;
}

/** Never carries a numeric or star rating — see CLAUDE.md's "hidden ratings" control. */
export interface CandidateEvaluationDto {
  reviewId: number;
  checkpoint: Checkpoint;
  submittedUtc: string;
  strengths: string | null;
  growthAreas: string | null;
  recommendConversion: boolean | null;
}

export interface CandidateDashboardDto {
  activeProject: MyAssignmentDto | null;
  tasksComplete: number;
  tasksTotal: number;
  matchScore: number | null;
  communityPostsThisWeek: number;
}

export interface CommunityCommentDto {
  communityCommentId: number;
  authorName: string;
  body: string;
  createdUtc: string;
}

export interface CommunityPostDto {
  communityPostId: number;
  authorName: string;
  authorRoleLabel: string | null;
  body: string;
  postType: CommunityPostType;
  createdUtc: string;
  likeCount: number;
  hasLikedByMe: boolean;
  comments: CommunityCommentDto[];
}

export interface CreateCommunityPostRequest {
  body: string;
  postType: CommunityPostType;
}

export interface CreateCommunityCommentRequest {
  body: string;
}

export type CohortStatus = 'Planned' | 'Active' | 'Completed';

export interface CohortDto {
  cohortId: number;
  programId: number;
  programName: string;
  name: string;
  startDate: string;
  endDate: string;
  status: CohortStatus;
  candidateCount: number;
  projectCount: number;
}

export interface CreateCohortRequest {
  name: string;
  startDate: string;
  endDate: string;
}

export interface PendingAssignmentDto {
  assignmentId: number;
  candidateId: number;
  candidateName: string;
  projectId: number;
  projectName: string;
  sponsorName: string;
  sponsorOrganization: string | null;
  matchScore: number | null;
  matchRationale: string | null;
}

export interface RunMatchingResult {
  proposedCount: number;
}

export interface MatchFunnelDto {
  proposed: number;
  approved: number;
  denied: number;
  active: number;
}

export interface RiskCandidateDto {
  candidateId: number;
  displayName: string;
  cohortName: string;
  avgScore: number | null;
  hasPerformanceRisk: boolean;
  hasEngagementRisk: boolean;
  staleTodoCount: number;
}

export interface OpsDashboardDto {
  activeCandidateCount: number;
  activeProjectCount: number;
  activeProjectCohortCount: number;
  pendingApprovalCount: number;
  approvedTotalCount: number;
  highRiskCount: number;
  matchFunnel: MatchFunnelDto;
  topRisks: RiskCandidateDto[];
}

export interface ProjectMatchDto {
  assignmentId: number;
  candidateId: number;
  candidateName: string;
  matchScore: number | null;
  matchRationale: string | null;
}

export interface SubmitReviewRequest {
  assignmentId: number;
  reviewType: 'SponsorOnCandidate' | 'CandidateOnSponsor' | 'ProjectEval';
  checkpoint: Checkpoint;
  commitment: number | null;
  availability: number | null;
  guidance: number | null;
  outputQuality: number | null;
  comments: string | null;
  strengths: string | null;
  growthAreas: string | null;
  recommendConversion: boolean | null;
}

/** Never carries OverallScore or the four numeric rating dimensions — see
 * CLAUDE.md's "hidden ratings" control. Not even the sponsor who submitted them
 * gets them echoed back. */
export interface SponsorReviewDto {
  reviewId: number;
  assignmentId: number;
  checkpoint: Checkpoint;
  submittedUtc: string;
  comments: string | null;
  strengths: string | null;
  growthAreas: string | null;
  recommendConversion: boolean | null;
}

export interface SponsorCandidateDto {
  assignmentId: number;
  candidateId: number;
  candidateName: string;
  projectId: number;
  projectName: string;
  status: AssignmentStatus;
  startDate: string | null;
}

export interface NotificationDto {
  notificationId: number;
  subject: string;
  body: string;
  isRead: boolean;
  createdUtc: string;
}

export interface SearchResultDto {
  type: 'Project' | 'Candidate';
  id: number;
  title: string;
  subtitle: string;
  url: string;
}
