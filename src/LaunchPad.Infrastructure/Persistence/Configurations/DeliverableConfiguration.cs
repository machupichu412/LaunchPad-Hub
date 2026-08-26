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

        // Optional, and set-null-on-delete at the EF Core (client) level rather than the DB
        // level — a deliverable should survive its to-do disappearing. Plain DeleteBehavior.
        // SetNull creates a DB-level ON DELETE SET NULL action, which SQL Server rejects here:
        // deleting an Assignment already cascades directly to Deliverable (the FK above, EF's
        // default Cascade for a required FK), and *also* cascades to ProjectTodo, which would
        // then try to SET NULL the same Deliverable rows — two paths reaching the same table,
        // which SQL Server refuses at CREATE TABLE/ALTER time ("may cause cycles or multiple
        // cascade paths"). ClientSetNull keeps the same effective behavior through EF Core's
        // own change tracker, with no DB-level cascade action to conflict.
        builder.HasOne(d => d.ProjectTodo)
            .WithMany()
            .HasForeignKey(d => d.ProjectTodoId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
