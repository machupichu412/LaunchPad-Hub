using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class ProjectInterestConfiguration : IEntityTypeConfiguration<ProjectInterest>
{
    public void Configure(EntityTypeBuilder<ProjectInterest> builder)
    {
        builder.ToTable("ProjectInterest");
        builder.HasKey(pi => pi.ProjectInterestId);
        builder.Property(pi => pi.RowVersion).IsRowVersion();

        builder.HasOne(pi => pi.Project)
            .WithMany(p => p.Interests)
            .HasForeignKey(pi => pi.ProjectId);

        builder.HasOne(pi => pi.Candidate)
            .WithMany()
            .HasForeignKey(pi => pi.CandidateId);

        // One rating per (Candidate, Project) — rating again upserts the existing row.
        builder.HasIndex(pi => new { pi.CandidateId, pi.ProjectId })
            .IsUnique()
            .HasDatabaseName("UX_ProjectInterest_Candidate_Project");
    }
}
