using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class CohortConfiguration : IEntityTypeConfiguration<Cohort>
{
    public void Configure(EntityTypeBuilder<Cohort> builder)
    {
        builder.ToTable("Cohort");
        builder.HasKey(c => c.CohortId);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();

        builder.HasOne(c => c.Program)
            .WithMany(p => p.Cohorts)
            .HasForeignKey(c => c.ProgramId);
    }
}
