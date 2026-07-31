using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Projects;

public interface IProjectRepository
{
    Task<Project?> GetWithSponsorAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetByCohortAsync(int cohortId, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetOpenByCohortAsync(int cohortId, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetBySponsorAsync(int sponsorId, CancellationToken ct = default);
    Task<Project> AddAsync(Project project, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
