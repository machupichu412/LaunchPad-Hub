using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class DeliverableConfiguration : IEntityTypeConfiguration<Deliverable>
{
    public void Configure(EntityTypeBuilder<Deliverable> builder)
    {
        builder.ToTable("Deliverable");
        builder.HasKey(d => d.DeliverableId);
        builder.Property(d => d.Title).HasMaxLength(300).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();
        builder.Property(d => d.SharePointItemId).HasMaxLength(300);

        builder.HasOne(d => d.Assignment)
            .WithMany(a => a.Deliverables)
            .HasForeignKey(d => d.AssignmentId);

        // Optional and explicitly SetNull (not Cascade) — a deliverable should survive its
        // to-do disappearing, and this keeps Deliverable off any multi-cascade-path collision
        // with the Assignment FK above (see AssignmentConfiguration's own Restrict note).
        builder.HasOne(d => d.ProjectTodo)
            .WithMany()
            .HasForeignKey(d => d.ProjectTodoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
