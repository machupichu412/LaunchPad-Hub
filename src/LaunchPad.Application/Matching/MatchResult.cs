namespace LaunchPad.Application.Matching;

public record MatchResult(int CandidateId, decimal Score, string Rationale);

public record MatchCandidate(int CandidateId, Domain.Enums.Availability Availability, IReadOnlyCollection<int> SkillIds);

public record MatchProject(int ProjectId, Domain.Enums.Availability AvailabilityNeeded, IReadOnlyCollection<(int SkillId, bool IsRequired)> Skills);
