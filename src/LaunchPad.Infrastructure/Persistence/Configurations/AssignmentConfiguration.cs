using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignment");
        builder.HasKey(a => a.AssignmentId);
        builder.Property(a => a.MatchScore).HasColumnType("decimal(5,2)");
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasOne(a => a.Project)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.ProjectId);

        builder.HasOne(a => a.Candidate)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.CandidateId);

        // One active assignment per candidate: Status IN (OpsApproved=2, Active=3).
        // Never bypass this with a raw insert — see CLAUDE.md.
        builder.HasIndex(a => a.CandidateId)
            .IsUnique()
            .HasDatabaseName("UX_Assignment_Active")
            .HasFilter("[Status] IN (2,3)");
    }
}
