/**
 * Synthetic fixture data + a tiny route resolver for mock mode (see mockMode.ts).
 * Every shape here mirrors api/types.ts exactly, so pages render as they would
 * against the real API — just without a live backend. Mutations are accepted and
 * (where easy) applied to the in-memory fixtures so the UI feels responsive across
 * a session, but nothing here is persisted past a page reload, and nothing here
 * ever reaches a network socket.
 */
import type {
  CandidateDashboardDto,
  CandidateDto,
  CandidateEvaluationDto,
  CohortDto,
  CommunityCommentDto,
  CommunityFeedPageDto,
  CommunityPostDto,
  DeliverableDto,
  ExecutiveDashboardDto,
  MyAssignmentDto,
  NotificationDto,
  OpsDashboardDto,
  ProjectDto,
  ProjectTodoDto,
  RiskCandidateDto,
  SponsorCandidateDto,
  SponsorCandidateMatchDto,
  SponsorDto,
} from '../api/types';

const now = () => new Date().toISOString();
const hoursAgo = (h: number) => new Date(Date.now() - h * 3_600_000).toISOString();
const daysAgo = (d: number) => hoursAgo(d * 24);

// --- Cohorts ---------------------------------------------------------------
const cohorts: CohortDto[] = [
  {
    cohortId: 1,
    programId: 1,
    programName: 'LaunchPad Fellowship',
    name: 'LP-2026-Spring',
    startDate: '2026-01-12',
    endDate: '2026-05-15',
    status: 'Active',
    candidateCount: 6,
    projectCount: 4,
    sharePointFolderWebUrl: null,
  },
  {
    cohortId: 2,
    programId: 1,
    programName: 'LaunchPad Fellowship',
    name: 'LP-2025-Fall',
    startDate: '2025-09-02',
    endDate: '2025-12-19',
    status: 'Completed',
    candidateCount: 9,
    projectCount: 5,
    sharePointFolderWebUrl: null,
  },
];

// --- Candidates --------------------------------------------------------------
const meCandidate: CandidateDto = {
  candidateId: 1,
  displayName: 'Priya Natarajan',
  email: 'priya@example.com',
  location: 'Ann Arbor, MI',
  availability: 'PartTime',
  graduationDate: '2026-05-01',
  linkedInUrl: 'https://www.linkedin.com/in/priya-example',
  portfolioUrl: 'https://priya.dev',
  bio: "CS junior who likes making dashboards less boring and pastries more numerous. Currently obsessed with data viz and cardamom cookies.",
  school: 'University of Michigan',
  degree: 'B.S. Computer Science',
  gpa: 3.8,
  skills: ['React', 'TypeScript', 'Data Visualization', 'Figma'],
  status: 'InProgress',
  outcome: 'In Progress',
  sharePointFolderWebUrl: null,
};

const rosterCandidates: CandidateDto[] = [
  meCandidate,
  {
    candidateId: 2, displayName: 'Marcus Webb', email: 'marcus@example.com', location: 'Detroit, MI',
    availability: 'FullTime', graduationDate: '2026-12-01', linkedInUrl: null, portfolioUrl: null,
    bio: 'Backend-leaning generalist. Will refactor your API for fun.', school: 'Wayne State University', degree: 'B.S. Information Systems',
    gpa: 3.4, skills: ['C#', 'ASP.NET Core', 'Azure', 'SQL'], status: 'InProgress', outcome: 'In Progress',
    averageScore: 4.3, hasPerformanceRisk: false, hasEngagementRisk: false, suggestedHireOutcome: 'Hire', sharePointFolderWebUrl: null,
  },
  {
    candidateId: 3, displayName: 'Ava Chen', email: 'ava@example.com', location: 'Ypsilanti, MI',
    availability: 'PartTime', graduationDate: '2027-05-01', linkedInUrl: null, portfolioUrl: 'https://ava.design',
    bio: 'Product design + a little front-end. Sketches wireframes in the margins of everything.', school: 'Eastern Michigan University', degree: 'B.F.A. Interaction Design',
    gpa: 3.9, skills: ['Figma', 'User Research', 'React', 'Accessibility'], status: 'InProgress', outcome: 'In Progress',
    averageScore: 4.8, hasPerformanceRisk: false, hasEngagementRisk: false, suggestedHireOutcome: 'TalentPlus', sharePointFolderWebUrl: null,
  },
  {
    candidateId: 4, displayName: 'Jordan Reyes', email: 'jordan@example.com', location: 'Ann Arbor, MI',
    availability: 'PartTime', graduationDate: '2026-05-01', linkedInUrl: null, portfolioUrl: null,
    bio: 'Data-curious, spreadsheet-fluent, learning Python one notebook at a time.', school: 'University of Michigan', degree: 'B.S. Statistics',
    gpa: 3.2, skills: ['Python', 'SQL', 'Power BI'], status: 'InProgress', outcome: 'In Progress',
    averageScore: 2.6, hasPerformanceRisk: true, hasEngagementRisk: true, suggestedHireOutcome: 'NoHire', sharePointFolderWebUrl: null,
  },
  {
    candidateId: 5, displayName: 'Sam Okafor', email: 'sam@example.com', location: 'Southfield, MI',
    availability: 'FullTime', graduationDate: '2026-05-01', linkedInUrl: null, portfolioUrl: null,
    bio: 'QA-minded engineer — happiest when a test suite goes red before it goes green.', school: 'Michigan State University', degree: 'B.S. Computer Science',
    gpa: 3.6, skills: ['Playwright', 'TypeScript', 'CI/CD'], status: 'InProgress', outcome: 'In Progress',
    averageScore: 4.0, hasPerformanceRisk: false, hasEngagementRisk: false, suggestedHireOutcome: 'Hire', sharePointFolderWebUrl: null,
  },
  {
    candidateId: 6, displayName: 'Lena Kowalski', email: 'lena@example.com', location: 'Ann Arbor, MI',
    availability: 'PartTime', graduationDate: '2027-12-01', linkedInUrl: null, portfolioUrl: null,
    bio: 'New to the program — excited and mildly caffeinated.', school: 'University of Michigan', degree: 'B.S. Computer Science',
    gpa: 3.7, skills: ['JavaScript', 'HTML/CSS'], status: 'InProgress', outcome: 'In Progress',
    sharePointFolderWebUrl: null,
  },
];

const dashboard: CandidateDashboardDto = {
  activeProject: {
    assignmentId: 1,
    status: 'Active',
    startDate: '2026-01-20',
    endDate: null,
    matchScore: 92,
    matchRationale: 'Strong overlap on React + data visualization, plus a shared interest in nonprofit impact work.',
    projectId: 1,
    projectName: 'Community Impact Dashboard',
    projectDescription: 'A real-time dashboard for tracking volunteer hours and program outcomes across three counties.',
    projectSkills: ['React', 'TypeScript', 'Data Visualization'],
    sponsorName: 'Alex Whitfield',
    sponsorOrganization: 'Contoso Nonprofit Alliance',
    tasksTotal: 5,
    tasksComplete: 2,
  },
  tasksComplete: 2,
  tasksTotal: 5,
  matchScore: 92,
  communityPostsThisWeek: 3,
};

const myAssignment: MyAssignmentDto = dashboard.activeProject!;

const todos: ProjectTodoDto[] = [
  { projectTodoId: 1, title: 'Wireframe the county-level filter view', status: 'Completed', priority: 'High', dueDate: '2026-02-01', linkedReviewType: null, linkedReviewCheckpoint: null },
  { projectTodoId: 2, title: 'Wire up the volunteer-hours API integration', status: 'Completed', priority: 'High', dueDate: '2026-02-10', linkedReviewType: null, linkedReviewCheckpoint: null },
  { projectTodoId: 3, title: 'Build the outcomes trendline chart', status: 'InProgress', priority: 'Medium', dueDate: '2026-02-24', linkedReviewType: null, linkedReviewCheckpoint: null },
  { projectTodoId: 4, title: 'Accessibility pass on the dashboard shell', status: 'NotStarted', priority: 'Medium', dueDate: '2026-03-03', linkedReviewType: null, linkedReviewCheckpoint: null },
  { projectTodoId: 5, title: 'Submit your Midpoint review of Alex Whitfield', status: 'NotStarted', priority: 'Low', dueDate: '2026-03-01', linkedReviewType: 'CandidateOnSponsor', linkedReviewCheckpoint: 'Midpoint' },
];

const deliverables: DeliverableDto[] = [
  { deliverableId: 1, title: 'Filter view wireframes', fileName: 'county-filter-wireframes.pdf', status: 'Submitted', submittedUtc: daysAgo(9), projectTodoId: 1, projectTodoTitle: 'Wireframe the county-level filter view', hasFile: true },
  { deliverableId: 2, title: 'API integration notes', fileName: 'volunteer-hours-api-notes.docx', status: 'Submitted', submittedUtc: daysAgo(3), projectTodoId: 2, projectTodoTitle: 'Wire up the volunteer-hours API integration', hasFile: true },
];

const evaluations: CandidateEvaluationDto[] = [
  {
    reviewId: 1, checkpoint: 'Midpoint', submittedUtc: daysAgo(5),
    strengths: 'Picks up new libraries fast and asks great clarifying questions before diving in.',
    growthAreas: 'Could push back earlier when a requirement seems ambiguous, rather than guessing.',
    recommendConversion: true,
  },
];

// --- Projects ----------------------------------------------------------------
const projects: ProjectDto[] = [
  {
    projectId: 1, cohortId: 1, sponsorId: 1, sponsorName: 'Alex Whitfield', name: 'Community Impact Dashboard',
    description: 'A real-time dashboard for tracking volunteer hours and program outcomes across three counties.',
    availabilityNeeded: 'PartTime', startDate: '2026-01-20', endDate: '2026-05-15', approvalStatus: 'Approved', status: 'InProgress',
    rejectionReason: null, sponsorTeamsLink: '', maxCandidates: 2, committedCandidateCount: 1, spotsRemaining: 1, myInterestRating: 5,
    deliveryStage: 'BusinessValueDocumented',
    requiredSkills: [
      { skillName: 'React', category: 'Frontend', isRequired: true },
      { skillName: 'Data Visualization', category: 'Frontend', isRequired: true },
      { skillName: 'TypeScript', category: 'Frontend', isRequired: false },
    ],
    sharePointFolderWebUrl: null,
  },
  {
    projectId: 2, cohortId: 1, sponsorId: 2, sponsorName: 'Dana Marsh', name: 'Volunteer Scheduling Bot',
    description: 'A Teams-integrated bot that matches volunteers to open shifts based on skills and availability.',
    availabilityNeeded: 'FullTime', startDate: '2026-01-20', endDate: '2026-05-15', approvalStatus: 'Approved', status: 'Open',
    rejectionReason: null, sponsorTeamsLink: '', maxCandidates: 2, committedCandidateCount: 0, spotsRemaining: 2, myInterestRating: null,
    deliveryStage: 'PilotReady',
    requiredSkills: [
      { skillName: 'C#', category: 'Backend', isRequired: true },
      { skillName: 'Bot Framework', category: 'Backend', isRequired: false },
    ],
    sharePointFolderWebUrl: null,
  },
  {
    projectId: 3, cohortId: 1, sponsorId: 1, sponsorName: 'Alex Whitfield', name: 'Donor Story Wall',
    description: 'A public-facing gallery of donor and volunteer stories, refreshed monthly.',
    availabilityNeeded: 'PartTime', startDate: '2026-01-20', endDate: '2026-05-15', approvalStatus: 'PendingOps', status: 'Open',
    rejectionReason: null, sponsorTeamsLink: '', maxCandidates: 1, committedCandidateCount: 0, spotsRemaining: 1, myInterestRating: null,
    deliveryStage: 'MvpBuilt',
    requiredSkills: [{ skillName: 'React', category: 'Frontend', isRequired: true }],
    sharePointFolderWebUrl: null,
  },
  {
    projectId: 4, cohortId: 1, sponsorId: 2, sponsorName: 'Dana Marsh', name: 'Legacy Intake Form Cleanup',
    description: 'Untangle a decade of intake-form spaghetti into one clean, accessible flow.',
    availabilityNeeded: 'PartTime', startDate: '2025-09-02', endDate: '2025-12-19', approvalStatus: 'Rejected', status: 'Cancelled',
    rejectionReason: 'Scope overlaps with an in-flight IT initiative — revisit next cohort.', sponsorTeamsLink: '', maxCandidates: 1,
    committedCandidateCount: 0, spotsRemaining: 1, myInterestRating: null, deliveryStage: 'NotStarted', requiredSkills: [], sharePointFolderWebUrl: null,
  },
];

const sponsorRoster: SponsorCandidateDto[] = [
  { assignmentId: 1, candidateId: 1, candidateName: 'Priya Natarajan', projectId: 1, projectName: 'Community Impact Dashboard', status: 'Active', startDate: '2026-01-20', sharePointFolderWebUrl: null },
];

// Eligible-candidate gallery for project 2 (Volunteer Scheduling Bot) — one candidate
// already batch-matched by Program Ops (proposedAssignmentId set) so the gallery's
// "Matched by Program Ops" section has something to render in mock mode, plus two
// plain eligible candidates below it.
const eligibleCandidates: SponsorCandidateMatchDto[] = [
  {
    candidateId: 2, displayName: 'Marcus Webb', location: 'Detroit, MI', availability: 'FullTime',
    graduationDate: '2026-12-01', bio: 'Backend-leaning generalist. Will refactor your API for fun.',
    school: 'Wayne State University', degree: 'B.S. Information Systems', gpa: 3.4,
    skills: ['C#', 'ASP.NET Core', 'Azure', 'SQL'], score: 91, rationale: 'Strong match on required C# and Bot Framework-adjacent backend skills.',
    interestRating: 4, hasPendingAssignmentElsewhere: false, proposedAssignmentId: 101,
  },
  {
    candidateId: 5, displayName: 'Sam Okafor', location: 'Southfield, MI', availability: 'FullTime',
    graduationDate: '2026-05-01', bio: 'QA-minded engineer — happiest when a test suite goes red before it goes green.',
    school: 'Michigan State University', degree: 'B.S. Computer Science', gpa: 3.6,
    skills: ['Playwright', 'TypeScript', 'CI/CD'], score: 62, rationale: 'Availability matches; limited overlap with required backend skills.',
    interestRating: null, hasPendingAssignmentElsewhere: false, proposedAssignmentId: null,
  },
  {
    candidateId: 3, displayName: 'Ava Chen', location: 'Ypsilanti, MI', availability: 'PartTime',
    graduationDate: '2027-05-01', bio: 'Product design + a little front-end. Sketches wireframes in the margins of everything.',
    school: 'Eastern Michigan University', degree: 'B.F.A. Interaction Design', gpa: 3.9,
    skills: ['Figma', 'User Research', 'React', 'Accessibility'], score: 38, rationale: 'Availability and skill set are a weak fit for this project.',
    interestRating: null, hasPendingAssignmentElsewhere: true, proposedAssignmentId: null,
  },
];

const mySponsor: SponsorDto = { sponsorId: 1, displayName: 'Alex Whitfield', organization: 'Contoso Nonprofit Alliance', title: 'Director of Technology' };

// --- Community -----------------------------------------------------------------
type MockPost = CommunityPostDto & { _comments: CommunityCommentDto[] };

const communityPosts: MockPost[] = [
  {
    communityPostId: 5, authorAppUserId: 6, authorName: 'Lena Kowalski', authorRoleLabel: 'Candidate', authorTeamsLink: '',
    body: "Just shipped my first real PR! 🎉 Small change, huge confidence boost. Thanks to everyone in #midpointreview thread who talked me through code review jitters.",
    postType: 'Win', createdUtc: hoursAgo(2), hasImage: false, likeCount: 14, hasLikedByMe: true, commentCount: 3,
    _comments: [
      { communityCommentId: 1, authorAppUserId: 3, authorName: 'Ava Chen', authorRoleLabel: 'Candidate', authorTeamsLink: '', body: 'YES. This is exactly the energy. 🙌', createdUtc: hoursAgo(1.8) },
      { communityCommentId: 2, authorAppUserId: 1, authorName: 'Priya Natarajan', authorRoleLabel: 'Candidate', authorTeamsLink: '', body: 'Remember this feeling for PR #2, it gets easier and also somehow better.', createdUtc: hoursAgo(1.2) },
      { communityCommentId: 3, authorAppUserId: 10, authorName: 'Morgan Lee', authorRoleLabel: 'Program Ops', authorTeamsLink: '', body: 'Logging this one for the highlight reel 📸', createdUtc: hoursAgo(0.5) },
    ],
  },
  {
    communityPostId: 4, authorAppUserId: 10, authorName: 'Morgan Lee', authorRoleLabel: 'Program Ops', authorTeamsLink: '',
    body: "Reminder: #midpointreview windows open this Friday for the Spring cohort. Sponsors + candidates will each get a couple of quick to-dos — takes 10 minutes, makes the rest of the program better. 💛",
    postType: 'Reminder', createdUtc: hoursAgo(6), hasImage: false, likeCount: 9, hasLikedByMe: false, commentCount: 1,
    _comments: [
      { communityCommentId: 4, authorAppUserId: 2, authorName: 'Marcus Webb', authorRoleLabel: 'Candidate', authorTeamsLink: '', body: 'On it — already blocked time for it 👍', createdUtc: hoursAgo(5) },
    ],
  },
  {
    communityPostId: 3, authorAppUserId: 1, authorName: 'Priya Natarajan', authorRoleLabel: 'Candidate', authorTeamsLink: '',
    body: "Quick question for the group — anyone found a good pattern for animating chart transitions without it feeling gimmicky? Trying to make the dashboard feel alive without being distracting. #datavis",
    postType: 'Question', createdUtc: daysAgo(1), hasImage: false, likeCount: 5, hasLikedByMe: false, commentCount: 2,
    _comments: [
      { communityCommentId: 5, authorAppUserId: 3, authorName: 'Ava Chen', authorRoleLabel: 'Candidate', authorTeamsLink: '', body: 'Keep durations under ~250ms and ease-out — anything longer starts to feel laggy rather than lively.', createdUtc: daysAgo(0.9) },
      { communityCommentId: 6, authorAppUserId: 11, authorName: 'Alex Whitfield', authorRoleLabel: 'Sponsor', authorTeamsLink: '', body: 'This is exactly the kind of polish that makes a demo land well — nice instinct.', createdUtc: daysAgo(0.7) },
    ],
  },
  {
    communityPostId: 2, authorAppUserId: 11, authorName: 'Alex Whitfield', authorRoleLabel: 'Sponsor', authorTeamsLink: '',
    body: "Kudos to the whole Community Impact Dashboard crew — the volunteer-hours integration landed a week early and it's already saving our ops team hours every Monday. 🙌 #shoutout",
    postType: 'Kudos', createdUtc: daysAgo(2), hasImage: false, likeCount: 21, hasLikedByMe: true, commentCount: 0, _comments: [],
  },
  {
    communityPostId: 1, authorAppUserId: 10, authorName: 'Morgan Lee', authorRoleLabel: 'Program Ops', authorTeamsLink: '',
    body: "Welcome to the LP-2026-Spring cohort! 🚀 Drop an intro below — what are you hoping to learn this term?",
    postType: 'Announcement', createdUtc: daysAgo(6), hasImage: false, likeCount: 12, hasLikedByMe: false, commentCount: 0, _comments: [],
  },
];

const notifications: NotificationDto[] = [
  { notificationId: 1, subject: 'Match proposed: Community Impact Dashboard', body: 'You were matched to a new project — take a look and rate your interest.', isRead: false, createdUtc: hoursAgo(20) },
  { notificationId: 2, subject: 'Midpoint reviews are open', body: 'Program Ops scheduled midpoint reviews for your cohort — check your Tasks.', isRead: false, createdUtc: hoursAgo(5) },
  { notificationId: 3, subject: "Sam commented on your post", body: '"Congrats, well earned!" on your Community post.', isRead: true, createdUtc: daysAgo(3) },
];

const opsDashboard: OpsDashboardDto = {
  activeCandidateCount: 6,
  activeProjectCount: 3,
  activeProjectCohortCount: 1,
  pendingApprovalCount: 1,
  approvedTotalCount: 3,
  highRiskCount: 1,
  matchFunnel: { proposed: 8, approved: 5, denied: 1, active: 4 },
  topRisks: [
    { candidateId: 4, displayName: 'Jordan Reyes', cohortName: 'LP-2026-Spring', avgScore: 2.6, hasPerformanceRisk: true, hasEngagementRisk: true, staleTodoCount: 3 },
  ],
};

const risks: RiskCandidateDto[] = opsDashboard.topRisks;

const executiveDashboardByCohort: Record<number, ExecutiveDashboardDto> = {
  1: {
    cohortId: 1, recommendedCount: 4, approvedCount: 3, hiredCount: 1, performanceRiskCount: 1, engagementRiskCount: 1,
    // 3 non-cancelled projects: BusinessValueDocumented, PilotReady, MvpBuilt.
    projectCount: 3, solutionsDeliveredCount: 1, mvpCompleteCount: 3, pilotReadyCount: 2, businessValueDocumentedCount: 1,
    hireReadyCandidateCount: 2, decidedCandidateCount: 3,
    averageSponsorRating: 4.3, sponsorRatingCount: 6,
    universityBreakdown: [
      { school: 'University of Michigan', candidateCount: 3 },
      { school: 'Wayne State University', candidateCount: 1 },
      { school: 'Michigan State University', candidateCount: 1 },
      { school: 'Eastern Michigan University', candidateCount: 1 },
    ],
  },
  2: {
    cohortId: 2, recommendedCount: 7, approvedCount: 7, hiredCount: 5, performanceRiskCount: 0, engagementRiskCount: 1,
    projectCount: 5, solutionsDeliveredCount: 4, mvpCompleteCount: 5, pilotReadyCount: 5, businessValueDocumentedCount: 4,
    hireReadyCandidateCount: 6, decidedCandidateCount: 7,
    averageSponsorRating: 4.6, sponsorRatingCount: 11,
    universityBreakdown: [
      { school: 'University of Michigan', candidateCount: 4 },
      { school: 'Michigan State University', candidateCount: 3 },
    ],
  },
};

// --- Route table ---------------------------------------------------------------
type Json = unknown;
type Handler = (match: RegExpMatchArray, url: URL, method: string, body: unknown) => Json | undefined;

const routes: { method: string; pattern: RegExp; handler: Handler }[] = [
  { method: 'GET', pattern: /^\/api\/candidates\/me$/, handler: () => meCandidate },
  { method: 'GET', pattern: /^\/api\/candidates\/me\/dashboard$/, handler: () => dashboard },
  { method: 'GET', pattern: /^\/api\/candidates\/cohort\/\d+$/, handler: () => rosterCandidates },
  { method: 'GET', pattern: /^\/api\/candidates$/, handler: () => rosterCandidates },
  { method: 'GET', pattern: /^\/api\/candidates\/(\d+)$/, handler: (m) => rosterCandidates.find((c) => c.candidateId === Number(m[1])) ?? rosterCandidates[0] },

  { method: 'GET', pattern: /^\/api\/cohorts$/, handler: () => cohorts },
  { method: 'POST', pattern: /^\/api\/cohorts$/, handler: () => cohorts[0] },
  { method: 'PATCH', pattern: /^\/api\/cohorts\/\d+\/status$/, handler: () => cohorts[0] },
  { method: 'POST', pattern: /^\/api\/cohorts\/\d+\/schedule-reviews$/, handler: () => ({ assignmentsScheduled: 3, todosCreated: 9 }) },

  { method: 'GET', pattern: /^\/api\/sponsors\/me$/, handler: () => mySponsor },
  { method: 'GET', pattern: /^\/api\/sponsors\/me\/candidates$/, handler: () => sponsorRoster },

  { method: 'GET', pattern: /^\/api\/projects\/mine$/, handler: () => projects.filter((p) => p.sponsorId === 1) },
  { method: 'GET', pattern: /^\/api\/projects\/open$/, handler: () => projects.filter((p) => p.approvalStatus === 'Approved') },
  { method: 'GET', pattern: /^\/api\/projects\/cohort\/\d+$/, handler: () => projects },
  { method: 'GET', pattern: /^\/api\/projects\/pending-approval$/, handler: () => projects.filter((p) => p.approvalStatus === 'PendingOps') },
  { method: 'GET', pattern: /^\/api\/projects\/(\d+)\/open-detail$/, handler: (m) => projects.find((p) => p.projectId === Number(m[1])) },
  { method: 'GET', pattern: /^\/api\/projects\/(\d+)\/eligible-candidates$/, handler: (m) => (Number(m[1]) === 2 ? eligibleCandidates : []) },
  { method: 'GET', pattern: /^\/api\/projects\/(\d+)\/assigned-candidates$/, handler: () => sponsorRoster },
  { method: 'GET', pattern: /^\/api\/projects\/(\d+)$/, handler: (m) => projects.find((p) => p.projectId === Number(m[1])) },
  { method: 'POST', pattern: /^\/api\/projects$/, handler: () => projects[0] },
  { method: 'PUT', pattern: /^\/api\/projects\/\d+$/, handler: (m) => projects.find((p) => p.projectId === Number(m[1])) ?? projects[0] },

  { method: 'GET', pattern: /^\/api\/assignments\/mine$/, handler: () => myAssignment },
  { method: 'GET', pattern: /^\/api\/assignments\/\d+\/todos$/, handler: () => todos },
  { method: 'GET', pattern: /^\/api\/assignments\/\d+\/deliverables$/, handler: () => deliverables },
  { method: 'GET', pattern: /^\/api\/assignments\/\d+\/evaluations$/, handler: () => evaluations },
  { method: 'PATCH', pattern: /^\/api\/assignments\/\d+\/todos\/\d+$/, handler: (m, _u) => todos.find((t) => String(t.projectTodoId) === m[0].split('/').pop()) ?? todos[0] },

  { method: 'GET', pattern: /^\/api\/community\/posts$/, handler: (_m, url) => {
    const hashtag = url.searchParams.get('hashtag')?.toLowerCase();
    const items = communityPosts
      .filter((p) => !hashtag || p.body.toLowerCase().includes(`#${hashtag}`))
      .map(({ _comments: _c, ...dto }) => dto);
    return { items, nextCursor: null } satisfies CommunityFeedPageDto;
  } },
  { method: 'GET', pattern: /^\/api\/community\/posts\/(\d+)\/comments$/, handler: (m) => communityPosts.find((p) => p.communityPostId === Number(m[1]))?._comments ?? [] },
  { method: 'POST', pattern: /^\/api\/community\/posts$/, handler: () => undefined },
  { method: 'POST', pattern: /^\/api\/community\/posts\/\d+\/comments$/, handler: () => ({ communityCommentId: 999, authorAppUserId: 1, authorName: 'Priya Natarajan', authorRoleLabel: 'Candidate', authorTeamsLink: '', body: 'New comment', createdUtc: now() } satisfies CommunityCommentDto) },
  { method: 'POST', pattern: /^\/api\/community\/posts\/\d+\/reactions$/, handler: () => ({ liked: true }) },
  { method: 'GET', pattern: /^\/api\/community\/posts\/\d+\/image$/, handler: () => undefined },

  { method: 'GET', pattern: /^\/api\/app-users\/\d+\/avatar$/, handler: () => undefined },
  { method: 'GET', pattern: /^\/api\/candidates\/\d+\/avatar$/, handler: () => undefined },
  { method: 'GET', pattern: /^\/api\/me\/avatar$/, handler: () => undefined },

  { method: 'GET', pattern: /^\/api\/notifications$/, handler: () => notifications },
  { method: 'GET', pattern: /^\/api\/notifications\/unread-count$/, handler: () => notifications.filter((n) => !n.isRead).length },
  { method: 'POST', pattern: /^\/api\/notifications\/\d+\/read$/, handler: () => ({}) },
  { method: 'POST', pattern: /^\/api\/notifications\/read-all$/, handler: () => ({}) },

  { method: 'GET', pattern: /^\/api\/ops\/dashboard$/, handler: () => opsDashboard },
  { method: 'GET', pattern: /^\/api\/ops\/risks$/, handler: () => risks },
  { method: 'GET', pattern: /^\/api\/ops\/executive-dashboard\/(\d+)$/, handler: (m) => executiveDashboardByCohort[Number(m[1])] ?? { cohortId: Number(m[1]), recommendedCount: 0, approvedCount: 0, hiredCount: 0, performanceRiskCount: 0, engagementRiskCount: 0 } },

  { method: 'GET', pattern: /^\/api\/search$/, handler: () => [] },

  { method: 'GET', pattern: /^\/api\/skills$/, handler: () => [
    { skillId: 1, name: 'React', skillCategoryId: 1, skillCategoryName: 'Frontend' },
    { skillId: 2, name: 'TypeScript', skillCategoryId: 1, skillCategoryName: 'Frontend' },
    { skillId: 3, name: 'C#', skillCategoryId: 2, skillCategoryName: 'Backend' },
    { skillId: 4, name: 'Data Visualization', skillCategoryId: 1, skillCategoryName: 'Frontend' },
  ] },
  { method: 'GET', pattern: /^\/api\/skills\/categories$/, handler: () => [
    { skillCategoryId: 1, name: 'Frontend' },
    { skillCategoryId: 2, name: 'Backend' },
  ] },

  { method: 'GET', pattern: /^\/api\/reviews\/assignment\/\d+$/, handler: () => [] },
  { method: 'POST', pattern: /^\/api\/reviews$/, handler: () => ({ reviewId: 1, assignmentId: 1, checkpoint: 'Midpoint', submittedUtc: now(), comments: null, strengths: null, growthAreas: null, recommendConversion: null }) },
];

function isListShaped(pathname: string): boolean {
  // Heuristic fallback for anything not explicitly routed above — a trailing
  // plural-ish collection segment (not ending in a specific /{id}) reads as a list.
  const lastSegment = pathname.split('/').filter(Boolean).pop() ?? '';
  return !/^\d+$/.test(lastSegment);
}

export async function resolveMock(method: string, rawUrl: string, body: unknown): Promise<{ status: number; body: Json }> {
  const url = new URL(rawUrl, 'http://mock.local');
  const pathname = url.pathname;

  for (const route of routes) {
    if (route.method !== method) continue;
    const match = pathname.match(route.pattern);
    if (!match) continue;

    const result = route.handler(match, url, method, body);
    if (result === undefined) return { status: 404, body: null };
    return { status: 200, body: result };
  }

  // Unmatched route — degrade gracefully rather than hard-crash the page, so
  // in-progress redesign work on a not-yet-mocked endpoint still renders.
  return { status: 200, body: isListShaped(pathname) ? [] : {} };
}
