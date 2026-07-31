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

        builder.HasOne(d => d.Assignment)
            .WithMany(a => a.Deliverables)
            .HasForeignKey(d => d.AssignmentId);
    }
}
