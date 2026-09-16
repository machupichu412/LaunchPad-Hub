using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Review");
        builder.HasKey(r => r.ReviewId);

        // Persisted computed column owned by the database — see launchpad-build-guide.md §4.4.
        // EF must never write to this; ValueGeneratedOnAddOrUpdate + the SQL formula keep it read-only here.
        builder.Property(r => r.OverallScore)
            .HasColumnType("decimal(5,2)")
            .HasComputedColumnSql(
                "CAST((ISNULL([Commitment],0)+ISNULL([Availability],0)+ISNULL([Guidance],0)+ISNULL([OutputQuality],0)) AS DECIMAL(5,2)) " +
                "/ NULLIF((CASE WHEN [Commitment] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [Availability] IS NULL THEN 0 ELSE 1 END) " +
                "+ (CASE WHEN [Guidance] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [OutputQuality] IS NULL THEN 0 ELSE 1 END), 0)",
                stored: true);

        builder.HasOne(r => r.Assignment)
            .WithMany(a => a.Reviews)
            .HasForeignKey(r => r.AssignmentId);

        builder.HasIndex(r => new { r.AssignmentId, r.ReviewType, r.Checkpoint, r.SubmittedBy })
            .IsUnique()
            .HasDatabaseName("UX_Review_Once");

        // Not redundant with UX_Review_Once: that index leads with AssignmentId, but the
        // hire-outcome lookup searches by type and checkpoint across every assignment a
        // candidate has had, then takes the most recent. SubmittedUtc trails so the sort
        // is satisfied by the index rather than a separate sort step.
        builder.HasIndex(r => new { r.ReviewType, r.Checkpoint, r.SubmittedUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Review_Type_Checkpoint_Submitted");
    }
}
