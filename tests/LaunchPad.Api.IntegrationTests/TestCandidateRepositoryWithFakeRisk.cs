using LaunchPad.Application.Candidates;
using LaunchPad.Domain.Entities;
using LaunchPad.Infrastructure.Persistence;
using LaunchPad.Infrastructure.Persistence.Repositories;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Wraps the real CandidateRepository, overriding only GetRiskAsync. CandidateRisk is
/// keyless (HasNoKey — it's backed by dbo.vCandidateRisk in prod), so it can never be
/// tracked via .Add() under EF Core, not just under a relational provider — there is
/// no way to seed real risk data in an integration test. Every other member delegates
/// to the real, EF-backed implementation so ownership/redaction logic is exercised
/// for real.
/// </summary>
public sealed class TestCandidateRepositoryWithFakeRisk : ICandidateRepository
{
    private readonly CandidateRepository _inner;
    public TestCandidateRepositoryWithFakeRisk(LaunchPadDbContext db) => _inner = new CandidateRepository(db);

    public Task<Candidate?> GetWithSkillsAsync(int candidateId, CancellationToken ct = default) =>
        _inner.GetWithSkillsAsync(candidateId, ct);

    public Task<Candidate?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default) =>
        _inner.GetByEntraObjectIdAsync(entraObjectId, ct);

    public Task<IReadOnlyList<Candidate>> GetByCohortAsync(int cohortId, CancellationToken ct = default) =>
        _inner.GetByCohortAsync(cohortId, ct);

    public Task<IReadOnlyList<Candidate>> GetByCohortsAsync(IReadOnlyList<int> cohortIds, CancellationToken ct = default) =>
        _inner.GetByCohortsAsync(cohortIds, ct);

    public Task<Candidate> AddAsync(Candidate candidate, CancellationToken ct = default) =>
        _inner.AddAsync(candidate, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);

    public Task<CandidateRisk?> GetRiskAsync(int candidateId, CancellationToken ct = default) =>
        Task.FromResult<CandidateRisk?>(new CandidateRisk
        {
            CandidateId = candidateId,
            AvgScore = 2.1m,
            HasPerformanceRisk = true,
            HasEngagementRisk = false,
        });
}
