using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps to dbo.vCandidateRisk (a database view, not a table) — keyless and read-only.
/// The view definition itself (see launchpad-build-guide.md §4.4) is applied via migration,
/// not by EF, since EF does not generate CREATE VIEW.
/// </summary>
public class CandidateRiskConfiguration : IEntityTypeConfiguration<CandidateRisk>
{
    public void Configure(EntityTypeBuilder<CandidateRisk> builder)
    {
        builder.ToView("vCandidateRisk");
        builder.HasNoKey();
    }
}
