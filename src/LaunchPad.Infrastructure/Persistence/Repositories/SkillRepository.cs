using LaunchPad.Application.Skills;
using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LaunchPad.Infrastructure.Persistence.Repositories;

public sealed class SkillRepository : ISkillRepository
{
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
            .Where(s => distinctNames.Contains(s.Name))
            .ToListAsync(ct);

        var missingNames = distinctNames
            .Where(n => !existing.Any(s => string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (missingNames.Length > 0)
        {
            var created = missingNames.Select(n => new Skill { Name = n }).ToArray();
            await _db.Skills.AddRangeAsync(created, ct);
            await _db.SaveChangesAsync(ct);
            existing.AddRange(created);
        }

        return existing;
    }
}
