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

    /// <summary>Every skill, with its category — the picker's full browsable list.</summary>
    Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SkillCategory>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Deliberate, category-driven creation (as opposed to the free-text
    /// "Uncategorized" fallback in GetOrCreateByNamesAsync) — for a picker where the
    /// caller explicitly chose a category. Idempotent: a case-insensitive name match
    /// returns the existing row rather than creating a duplicate, ignoring whatever
    /// category was submitted this time.</summary>
    Task<Skill> CreateAsync(string name, int skillCategoryId, CancellationToken ct = default);
}
