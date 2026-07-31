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

export type ProjectApprovalStatus = 'Draft' | 'PendingOps' | 'Approved' | 'Rejected';
export type ProjectStatus = 'Open' | 'InProgress' | 'Completed' | 'Cancelled';

export interface ProjectSkillDto {
  skillName: string;
  isRequired: boolean;
}

export interface ProjectDto {
  projectId: number;
  cohortId: number;
  sponsorId: number;
  name: string;
  description: string | null;
  availabilityNeeded: Availability;
  startDate: string | null;
  endDate: string | null;
  approvalStatus: ProjectApprovalStatus;
  status: ProjectStatus;
  requiredSkills: ProjectSkillDto[];
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
