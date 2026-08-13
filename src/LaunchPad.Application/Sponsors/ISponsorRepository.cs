using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Sponsors;

public interface ISponsorRepository
{
    Task<Sponsor?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);
    Task<Sponsor> AddAsync(Sponsor sponsor, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
