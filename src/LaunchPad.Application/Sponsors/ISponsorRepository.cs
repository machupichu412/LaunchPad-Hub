using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Sponsors;

public interface ISponsorRepository
{
    Task<Sponsor?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);
}
