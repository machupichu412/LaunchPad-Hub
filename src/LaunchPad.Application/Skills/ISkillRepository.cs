using LaunchPad.Domain.Entities;

namespace LaunchPad.Application.Skills;

public interface ISkillRepository
{
    /// <summary>
    /// Case-insensitive exact-name match, creating rows for any names that don't
    /// exist yet. This is the naive form of the normalization CLAUDE.md calls for —
    /// real free-text dedup ("Power BI" / "PowerBI") is migration/taxonomy work,
    /// not runtime logic.
    /// </summary>
    Task<IReadOnlyList<Skill>> GetOrCreateByNamesAsync(IEnumerable<string> names, CancellationToken ct = default);
}
