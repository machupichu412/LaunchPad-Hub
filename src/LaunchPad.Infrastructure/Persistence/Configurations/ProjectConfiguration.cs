using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Project");
        builder.HasKey(p => p.ProjectId);
        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.RejectionReason).HasMaxLength(1000);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasOne(p => p.Cohort)
            .WithMany(c => c.Projects)
            .HasForeignKey(p => p.CohortId);

        builder.HasOne(p => p.Sponsor)
            .WithMany(s => s.Projects)
            .HasForeignKey(p => p.SponsorId);
    }
}
