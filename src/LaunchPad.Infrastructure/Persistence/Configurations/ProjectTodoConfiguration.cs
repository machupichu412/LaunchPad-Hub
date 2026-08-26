using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class ProjectTodoConfiguration : IEntityTypeConfiguration<ProjectTodo>
{
    public void Configure(EntityTypeBuilder<ProjectTodo> builder)
    {
        builder.ToTable("ProjectTodo");
        builder.HasKey(t => t.ProjectTodoId);
        builder.Property(t => t.Title).HasMaxLength(300).IsRequired();

        builder.HasOne(t => t.Assignment)
            .WithMany(a => a.Todos)
            .HasForeignKey(t => t.AssignmentId);

        // Idempotency guard for cohort-wide review scheduling (CohortsController.ScheduleReviews) —
        // re-scheduling the same checkpoint can't duplicate a candidate/sponsor's linked to-do.
        // Ordinary (non-review) to-dos have LinkedReviewType == null and are excluded by the filter.
        builder.HasIndex(t => new { t.AssignmentId, t.LinkedReviewType, t.LinkedReviewCheckpoint })
            .IsUnique()
            .HasDatabaseName("UX_ProjectTodo_LinkedReview_Once")
            .HasFilter("[LinkedReviewType] IS NOT NULL");
    }
}
