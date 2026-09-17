using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("Candidate");
        builder.HasKey(c => c.CandidateId);
        builder.Property(c => c.Location).HasMaxLength(100);
        builder.Property(c => c.LinkedInUrl).HasMaxLength(500);
        builder.Property(c => c.PortfolioUrl).HasMaxLength(500);
        builder.Property(c => c.ResumeBlobPath).HasMaxLength(500);
        builder.Property(c => c.SharePointFolderId).HasMaxLength(300);
        builder.Property(c => c.SharePointFolderWebUrl).HasMaxLength(1000);
        builder.Property(c => c.Gpa).HasPrecision(3, 2);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasOne(c => c.AppUser)
            .WithMany()
            .HasForeignKey(c => c.AppUserId);

        builder.HasOne(c => c.Cohort)
            .WithMany(co => co.Candidates)
            .HasForeignKey(c => c.CohortId);

        builder.HasIndex(c => new { c.AppUserId, c.CohortId }).IsUnique();

        // Cohort-scoped status reporting (executive dashboard hire counts, hire-ready and
        // decided breakdowns). Also narrow enough to scan for the Ops dashboard's
        // cohort-less InProgress count, which is why that doesn't get its own index.
        builder.HasIndex(c => new { c.CohortId, c.Status })
            .HasDatabaseName("IX_Candidate_Cohort_Status");
    }
}
