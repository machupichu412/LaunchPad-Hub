// Hand-written placeholder types. Once the API's OpenAPI doc is published, replace
// this file with a generated client (nswag or openapi-typescript-codegen) so DTOs
// can't drift from the backend — see launchpad-build-guide.md §7.1.

export type Availability = 'PartTime' | 'FullTime';

export interface CandidateDto {
  candidateId: number;
  displayName: string;
  location: string | null;
  availability: Availability;
  graduationDate: string | null;
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
