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
    }
}
