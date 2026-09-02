using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps to dbo.vProjectDeliveryKpi (a database view, not a table) — keyless and read-only,
/// same pattern as CandidateRiskConfiguration. The view definition itself is applied via
/// migration, not by EF, since EF does not generate CREATE VIEW.
/// </summary>
public class ProjectDeliveryKpiConfiguration : IEntityTypeConfiguration<ProjectDeliveryKpi>
{
    public void Configure(EntityTypeBuilder<ProjectDeliveryKpi> builder)
    {
        builder.ToView("vProjectDeliveryKpi");
        builder.HasNoKey();
    }
}
