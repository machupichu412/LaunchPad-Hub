using LaunchPad.Application.Skills;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class SkillRepository : ISkillRepository
{
    private const string UncategorizedCategoryName = "Uncategorized";

    private readonly LaunchPadDbContext _db;
    public SkillRepository(LaunchPadDbContext db) => _db = db;

    public async Task<IReadOnlyList<Skill>> GetOrCreateByNamesAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        var distinctNames = names
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctNames.Length == 0) return Array.Empty<Skill>();

        var existing = await _db.Skills
            .Include(s => s.SkillCategory)
            .Where(s => distinctNames.Contains(s.Name))
            .ToListAsync(ct);

        var missingNames = distinctNames
            .Where(n => !existing.Any(s => string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (missingNames.Length > 0)
        {
            // Every skill needs a category (see SkillConfiguration); a skill typed
            // free-text on a Project/Candidate form has no category to offer, so it
            // falls back to a shared "Uncategorized" row that Program Ops can later
            // recategorize from a skills admin view.
            var uncategorizedId = await GetOrCreateUncategorizedCategoryIdAsync(ct);
            var created = missingNames.Select(n => new Skill { Name = n, SkillCategoryId = uncategorizedId }).ToArray();
            await _db.Skills.AddRangeAsync(created, ct);
            await _db.SaveChangesAsync(ct);
            existing.AddRange(created);
        }

        return existing;
    }

    public async Task<IReadOnlyList<Skill>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Skills
            .Include(s => s.SkillCategory)
            .OrderBy(s => s.SkillCategory.Name).ThenBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SkillCategory>> GetCategoriesAsync(CancellationToken ct = default) =>
        await _db.SkillCategories.OrderBy(sc => sc.Name).ToListAsync(ct);

    public async Task<Skill> CreateAsync(string name, int skillCategoryId, CancellationToken ct = default)
    {
        var trimmedName = name.Trim();
        var trimmedNameLower = trimmedName.ToLowerInvariant();

        // Case-insensitive, matching GetOrCreateByNamesAsync's dedup — SQL Server's
        // default collation is already case-insensitive, but the in-memory provider
        // (local demo, tests) isn't, so this can't rely on that.
        var existing = await _db.Skills
            .Include(s => s.SkillCategory)
            .FirstOrDefaultAsync(s => s.Name.ToLower() == trimmedNameLower, ct);
        if (existing is not null) return existing;

        var skill = new Skill { Name = trimmedName, SkillCategoryId = skillCategoryId };
        await _db.Skills.AddAsync(skill, ct);
        await _db.SaveChangesAsync(ct);

        // Reload with the category included so the caller can build a full SkillDto
        // without a second round trip.
        await _db.Entry(skill).Reference(s => s.SkillCategory).LoadAsync(ct);
        return skill;
    }

    private async Task<int> GetOrCreateUncategorizedCategoryIdAsync(CancellationToken ct)
    {
        var category = await _db.SkillCategories.FirstOrDefaultAsync(sc => sc.Name == UncategorizedCategoryName, ct);
        if (category is not null) return category.SkillCategoryId;

        category = new SkillCategory { Name = UncategorizedCategoryName };
        await _db.SkillCategories.AddAsync(category, ct);
        await _db.SaveChangesAsync(ct);
        return category.SkillCategoryId;
    }
}
