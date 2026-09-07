/**
 * The Program Ops guides, as data.
 *
 * Written for a non-technical audience: everything here is something Ops can do
 * from inside LaunchPad or from the SharePoint site they already administer.
 * Nothing in these guides asks anyone to open the Azure portal — where a task
 * genuinely requires it, the guide says who to ask instead (see `escalation`).
 *
 * Content lives here rather than in the page component so it can be edited
 * without touching layout code, and so the search index below stays derivable
 * from the same source. Every claim should stay true to the app's actual
 * behavior — if a workflow changes, update the matching guide in the same PR.
 */

export type HelpBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'heading'; text: string }
  | { kind: 'bullets'; items: string[] }
  | { kind: 'steps'; items: string[] }
  | { kind: 'path'; segments: string[]; caption?: string }
  | { kind: 'table'; columns: string[]; rows: string[][] }
  | { kind: 'callout'; tone: 'info' | 'caution'; title: string; text: string }
  | { kind: 'link'; to: string; label: string; description: string };

export interface HelpArticle {
  id: string;
  title: string;
  summary: string;
  /** Named for the `@fluentui/react-icons` component the page maps it to. */
  icon: 'compass' | 'people' | 'folder' | 'checkmark' | 'tag' | 'cloud' | 'warning' | 'question';
  blocks: HelpBlock[];
}

export const helpArticles: HelpArticle[] = [
  {
    id: 'how-launchpad-is-organized',
    title: 'How LaunchPad is organized',
    summary: 'The four things everything else hangs off — cohorts, candidates, sponsors, and projects — and who can see what.',
    icon: 'compass',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Almost every screen in LaunchPad is scoped to a cohort. A cohort is one run of the program: it holds its own candidates, its own sponsors, and its own projects. Nothing is shared across cohorts, so a candidate in the spring cohort never appears in the fall cohort\'s pipeline.',
      },
      {
        kind: 'table',
        columns: ['Thing', 'What it is', 'Who creates it'],
        rows: [
          ['Cohort', 'One run of the program, with a start and end date', 'Program Ops'],
          ['Candidate', 'A participant, with a profile and a skill list', 'The candidate, during onboarding'],
          ['Sponsor', 'The person who owns a project and reviews the candidate on it', 'The sponsor, during onboarding'],
          ['Project', 'A piece of work a candidate can be staffed to', 'A sponsor, then approved by Ops'],
          ['Assignment', 'The link between one candidate and one project', 'The matching engine or a sponsor request'],
        ],
      },
      { kind: 'heading', text: 'Roles' },
      {
        kind: 'paragraph',
        text: 'What someone sees is decided by their role, not by what they were sent a link to. A person can hold more than one role — the role switcher in the top bar changes which version of the app they are looking at.',
      },
      {
        kind: 'bullets',
        items: [
          'Candidate — their own profile, the project marketplace, their tasks and evaluations.',
          'Sponsor — their own projects and the candidates staffed to them.',
          'Program Ops — everything in this guide: cohorts, approvals, skills, risks.',
          'Executive — the dashboards and the talent pipeline, read-only.',
          'Hiring Manager — the talent pipeline only.',
        ],
      },
      {
        kind: 'callout',
        tone: 'info',
        title: 'Ratings stay hidden from sponsors and candidates',
        text: 'Numeric scores and risk flags are only ever sent to Program Ops and Executive accounts. A sponsor cannot see them by changing a link or a filter — the app leaves them out of the response entirely. You do not need to redact anything by hand.',
      },
    ],
  },

  {
    id: 'running-a-cohort',
    title: 'Running a cohort',
    summary: 'Create a cohort, move it through its three states, and schedule the midpoint and final reviews.',
    icon: 'people',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Creating the cohort is the first thing that happens in a new program run — candidates and sponsors cannot onboard into a cohort that does not exist yet.',
      },
      { kind: 'heading', text: 'Create the cohort' },
      {
        kind: 'steps',
        items: [
          'Go to Cohorts and choose New cohort.',
          'Give it a name people will recognize on a report a year from now — "Fall 2026", not "Cohort 3".',
          'Set the start and end dates. These drive the midpoint the review scheduler uses.',
          'Save. LaunchPad creates the cohort\'s SharePoint folder for you in the background; it usually appears within a minute.',
        ],
      },
      {
        kind: 'callout',
        tone: 'caution',
        title: 'The name becomes a folder name',
        text: 'The cohort name is used to name its SharePoint folder, so renaming a cohort after documents have been filed in it will leave the old folder behind under the old name. Get the name right at creation time.',
      },
      { kind: 'heading', text: 'The three cohort states' },
      {
        kind: 'table',
        columns: ['State', 'What it means', 'Move to it when'],
        rows: [
          ['Planned', 'Being set up. Sponsors can draft projects.', 'The cohort is created'],
          ['Active', 'Running. Matching and staffing happen here.', 'Onboarding closes and you are ready to staff'],
          ['Completed', 'Finished. Kept for reporting and history.', 'Final reviews are in'],
        ],
      },
      { kind: 'heading', text: 'Schedule reviews' },
      {
        kind: 'paragraph',
        text: 'Reviews are not automatic — you schedule each round. From Cohorts, choose Schedule reviews on the cohort, pick Midpoint or Final, and set a due date.',
      },
      {
        kind: 'paragraph',
        text: 'One round creates a to-do for every active assignment in the cohort: each candidate gets one for their sponsor and one for their project, and each sponsor gets one for their candidate. The confirmation tells you how many to-dos were created — if that number looks low, check that the assignments you expected are actually Active.',
      },
      {
        kind: 'link',
        to: '/ops/cohorts',
        label: 'Open Cohorts',
        description: 'Create cohorts, change state, and schedule review rounds.',
      },
    ],
  },

  {
    id: 'projects-and-approvals',
    title: 'Approving projects',
    summary: 'Review what sponsors submit before it reaches candidates in the marketplace.',
    icon: 'folder',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Sponsors write their own projects, but nothing reaches the candidate marketplace until Ops approves it. Project Approvals is the queue of everything waiting on you.',
      },
      { kind: 'heading', text: 'What to check before approving' },
      {
        kind: 'bullets',
        items: [
          'The description says what the candidate will actually do, not just what the team does.',
          'The required skills are real requirements. Everything marked required narrows the candidate pool, so an over-specified project matches nobody.',
          'The skills are picked from the existing list rather than near-duplicates of it — see the skills guide.',
          'There is a named sponsor who will genuinely be available to review the candidate.',
        ],
      },
      {
        kind: 'paragraph',
        text: 'Approving publishes the project to the marketplace and makes it eligible for matching. Rejecting sends it back to the sponsor with your reason, so write the reason as something they can act on — "needs 2–3 concrete deliverables" rather than "not enough detail".',
      },
      {
        kind: 'callout',
        tone: 'info',
        title: 'Approving a project creates its folder',
        text: 'Each approved project gets its own SharePoint folder automatically. You do not need to create one.',
      },
      {
        kind: 'link',
        to: '/ops/project-approvals',
        label: 'Open Project Approvals',
        description: 'The queue of projects sponsors have submitted.',
      },
    ],
  },

  {
    id: 'matching-and-assignments',
    title: 'Matching and staffing candidates',
    summary: 'Run the matching engine, read a match score, and approve the assignments that come out of it.',
    icon: 'checkmark',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Matching compares each candidate\'s skills against each approved project\'s required skills and proposes pairings with a score and a short rationale. It is a shortlist, not a decision — every match still passes through a sponsor and then through you.',
      },
      { kind: 'heading', text: 'How an assignment moves' },
      {
        kind: 'table',
        columns: ['Stage', 'What just happened', 'Who acts next'],
        rows: [
          ['Proposed', 'Matching suggested the pairing', 'The sponsor'],
          ['Sponsor approved', 'The sponsor wants this candidate', 'Program Ops'],
          ['Ops approved', 'You cleared it', 'Nobody — it starts'],
          ['Active', 'The candidate is working on it', 'Reviews happen here'],
          ['Completed / Withdrawn', 'It ended, either way', '—'],
        ],
      },
      { kind: 'heading', text: 'Running matching' },
      {
        kind: 'steps',
        items: [
          'Make sure the projects you want considered are approved and the candidates have finished onboarding — matching only sees completed profiles.',
          'Go to Assignment Approvals and choose Run matching.',
          'Matching runs in the background. The queue does not populate instantly; give it a moment and refresh.',
          'Work the queue. Approving staffs the candidate; denying returns them to the pool for a later round.',
        ],
      },
      {
        kind: 'callout',
        tone: 'caution',
        title: 'One active assignment per candidate',
        text: 'A candidate can only be actively staffed to one project at a time — the app rejects a second one rather than quietly double-booking. If an approval is refused for this reason, the candidate is already staffed somewhere; find their current assignment first.',
      },
      {
        kind: 'paragraph',
        text: 'You can re-run matching as often as you like. It proposes new pairings for candidates who are not yet staffed and does not disturb assignments that are already active.',
      },
      {
        kind: 'link',
        to: '/ops/approvals',
        label: 'Open Assignment Approvals',
        description: 'Run matching and work the queue of sponsor-approved matches.',
      },
    ],
  },

  {
    id: 'skills-taxonomy',
    title: 'Managing the skills list',
    summary: 'The shared vocabulary that matching depends on — and the one thing worth keeping tidy.',
    icon: 'tag',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Candidates pick their skills from a fixed list, and sponsors pick a project\'s required skills from the same list. That shared list is what makes matching work. It is the single most valuable thing Ops maintains in the app.',
      },
      { kind: 'heading', text: 'Adding a skill' },
      {
        kind: 'steps',
        items: [
          'Search the existing list first. Most requests are an existing skill under a different name.',
          'If it is genuinely new, pick the category it belongs to and add it.',
          'Use the name people would search for, spelled the way the industry spells it — "Power BI", not "PowerBI" or "power-bi".',
        ],
      },
      {
        kind: 'callout',
        tone: 'caution',
        title: 'Near-duplicates quietly break matching',
        text: 'If half the candidates tag "Power BI" and half tag "PowerBI", the engine treats them as two unrelated skills and neither group matches a project asking for the other. Adding a variant is not harmless — always search before you add.',
      },
      { kind: 'heading', text: 'Removing a skill' },
      {
        kind: 'paragraph',
        text: 'A skill can only be removed if nobody is using it. If a candidate profile or a project lists it, the app refuses the delete rather than silently stripping it from their profiles. To retire a skill that is in use, first move the people and projects using it onto the skill you are keeping.',
      },
      {
        kind: 'link',
        to: '/ops/skills',
        label: 'Open Skills',
        description: 'Browse the taxonomy by category, add skills, and retire unused ones.',
      },
    ],
  },

  {
    id: 'sharepoint-setup',
    title: 'SharePoint document library',
    summary: 'The folder structure LaunchPad expects, what it creates for you, and the two things not to touch.',
    icon: 'cloud',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Candidate deliverables live in SharePoint, not inside LaunchPad. LaunchPad files them into a folder structure it creates and maintains itself — your job is to own the site and its permissions, not to build the folders.',
      },
      { kind: 'heading', text: 'The structure LaunchPad creates' },
      { kind: 'path', segments: ['LaunchPad', 'Fall 2026', 'Candidates', 'Jordan Avery'], caption: 'Where one candidate\'s deliverables are filed' },
      { kind: 'path', segments: ['LaunchPad', 'Fall 2026', 'Projects', 'Supply Chain Dashboard'], caption: 'Where one project\'s materials are filed' },
      {
        kind: 'paragraph',
        text: 'A cohort folder is created when you create the cohort, a candidate folder when they finish onboarding, and a project folder when you approve the project. If a folder is ever missing, LaunchPad recreates it the next time it needs it — a missing folder is self-healing and not something to fix by hand.',
      },
      { kind: 'heading', text: 'What Ops owns' },
      {
        kind: 'bullets',
        items: [
          'Site permissions — who can open the library at all.',
          'Retention and any records policy the program is subject to.',
          'Storage capacity, and archiving cohorts that have long since completed.',
        ],
      },
      {
        kind: 'callout',
        tone: 'caution',
        title: 'Do not rename or move the folders LaunchPad created',
        text: 'LaunchPad remembers each folder by its identity in SharePoint, so moving one usually survives — but renaming a cohort, candidate, or project in LaunchPad afterwards will create a fresh folder under the new name rather than reusing the renamed one. If a folder needs a new name, change the name in LaunchPad and let it create the folder.',
      },
      {
        kind: 'callout',
        tone: 'info',
        title: 'Illegal characters are handled for you',
        text: 'SharePoint rejects characters like : / \\ * ? " < > and | in folder names. If a cohort or project name contains one, LaunchPad replaces it with a hyphen when it creates the folder — so the folder name may differ slightly from the name shown in the app. That is expected.',
      },
    ],
  },

  {
    id: 'risk-signals',
    title: 'Reading the risk list',
    summary: 'What puts a candidate on the risk list, and what the two kinds of risk actually mean.',
    icon: 'warning',
    blocks: [
      {
        kind: 'paragraph',
        text: 'The Risks page is calculated, not set by hand. Nobody flags a candidate as at-risk; they appear because their data matches one of two patterns, and they leave the list on their own when it stops matching.',
      },
      {
        kind: 'table',
        columns: ['Signal', 'What triggers it', 'What it usually means'],
        rows: [
          [
            'Performance risk',
            'A low average review score, or a score that dropped from midpoint to final',
            'The trajectory matters more than the number — a drop is worth a conversation even when the average still looks fine',
          ],
          [
            'Engagement risk',
            'To-dos left overdue, or a long stretch with no activity',
            'Often a availability or expectations problem with the placement rather than the candidate',
          ],
        ],
      },
      {
        kind: 'callout',
        tone: 'info',
        title: 'The list is a prompt, not a verdict',
        text: 'A candidate on this list needs a conversation, not a decision. Scores here are visible to you and to Executives only — never repeat a numeric score to the candidate or their sponsor.',
      },
      {
        kind: 'link',
        to: '/ops/risks',
        label: 'Open Risks',
        description: 'The current at-risk list for the active cohort.',
      },
    ],
  },

  {
    id: 'escalation',
    title: 'When to ask for help',
    summary: 'The handful of things Ops cannot fix from inside the app, and who owns each of them.',
    icon: 'question',
    blocks: [
      {
        kind: 'paragraph',
        text: 'Everything in the other guides is yours to run. This page covers the exceptions — the things that need an administrator, so you do not spend an afternoon looking for a setting that is not there.',
      },
      {
        kind: 'table',
        columns: ['Situation', 'Who owns it'],
        rows: [
          ['Someone signs in but sees the wrong screens, or no screens', 'IT — their access group membership'],
          ['A new Ops or Executive needs access', 'IT — add them to the right access group'],
          ['Nobody can sign in at all', 'IT / the LaunchPad engineering team'],
          ['The SharePoint site itself needs to be created or moved', 'The LaunchPad engineering team'],
          ['Deliverables fail to upload for everyone', 'The LaunchPad engineering team'],
          ['A report or dashboard shows numbers you cannot explain', 'The LaunchPad engineering team'],
        ],
      },
      {
        kind: 'callout',
        tone: 'caution',
        title: 'Access is managed by group, not by person',
        text: 'People get into LaunchPad by being in an access group, and roles come from that membership. There is no user list inside LaunchPad to add someone to — asking IT to add them to the group is the whole process, and removing them from it removes their access everywhere at once.',
      },
      { kind: 'heading', text: 'What to include when you report a problem' },
      {
        kind: 'bullets',
        items: [
          'Who hit it — the person\'s work email, and which role they were viewing as.',
          'What page they were on, and what they had just clicked.',
          'What happened instead of what they expected.',
          'Roughly when, including the time zone. It narrows the search enormously.',
        ],
      },
    ],
  },
];

export function findArticle(id: string | undefined): HelpArticle | undefined {
  return helpArticles.find((article) => article.id === id);
}

/**
 * Flattens an article's blocks into one searchable string. Kept here next to the
 * content so a new block kind can't silently drop out of search.
 */
export function articleSearchText(article: HelpArticle): string {
  const fromBlocks = article.blocks.flatMap((block) => {
    switch (block.kind) {
      case 'paragraph':
      case 'heading':
        return [block.text];
      case 'bullets':
      case 'steps':
        return block.items;
      case 'path':
        return [...block.segments, block.caption ?? ''];
      case 'table':
        return [...block.columns, ...block.rows.flat()];
      case 'callout':
        return [block.title, block.text];
      case 'link':
        return [block.label, block.description];
    }
  });

  return [article.title, article.summary, ...fromBlocks].join(' ').toLowerCase();
}
