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

        // Restrict, not the default Cascade: Cohort cascades to both Candidate and
        // Project, and Candidate already cascades to Assignment — a second cascade
        // path in from Project would give Assignment two cascade paths back to
        // Cohort, which SQL Server rejects outright at migration time (Error 1785,
        // "may cause cycles or multiple cascade paths"). Deleting a Project should
        // never have silently wiped Assignment/Review/Deliverable history anyway.
        builder.HasOne(a => a.Project)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Candidate)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.CandidateId);

        // One active assignment per candidate: Status IN (OpsApproved=2, Active=3).
        // Never bypass this with a raw insert — see CLAUDE.md.
        builder.HasIndex(a => a.CandidateId)
            .IsUnique()
            .HasDatabaseName("UX_Assignment_Active")
            .HasFilter("[Status] IN (2,3)");

        // UX_Assignment_Active above is filtered to Status IN (2,3), so it cannot serve a
        // lookup for Proposed/SponsorApproved — which is exactly what the candidate's
        // pending-request list asks for (GetPendingAssignmentsForCandidateAsync).
        builder.HasIndex(a => new { a.CandidateId, a.Status })
            .HasDatabaseName("IX_Assignment_Candidate_Status");

        // The project-scoped status queries: proposed matches, committed roster, and the
        // committed-count check that gates over-assignment.
        builder.HasIndex(a => new { a.ProjectId, a.Status })
            .HasDatabaseName("IX_Assignment_Project_Status");

        // Ops dashboard counts assignments by status across every project, with no other
        // predicate to narrow on.
        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_Assignment_Status");
    }
}
