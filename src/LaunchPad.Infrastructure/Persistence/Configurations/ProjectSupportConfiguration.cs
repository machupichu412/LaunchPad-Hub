using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class ProjectExpertConfiguration : IEntityTypeConfiguration<ProjectExpert>
{
    public void Configure(EntityTypeBuilder<ProjectExpert> b)
    {
        b.HasKey(x => new { x.ProjectId, x.AppUserId });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.AppUser).WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
public class ProjectQuestionConfiguration : IEntityTypeConfiguration<ProjectQuestion>
{
    public void Configure(EntityTypeBuilder<ProjectQuestion> b)
    {
        b.HasKey(x => x.ProjectQuestionId);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Body).HasMaxLength(10000);
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorAppUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Deliverable).WithMany().HasForeignKey(x => x.DeliverableId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.ProjectId, x.CreatedUtc });
    }
}
public class ProjectAnswerConfiguration : IEntityTypeConfiguration<ProjectAnswer>
{
    public void Configure(EntityTypeBuilder<ProjectAnswer> b)
    {
        b.HasKey(x => x.ProjectAnswerId);
        b.Property(x => x.Body).HasMaxLength(10000);
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.FileItemId).HasMaxLength(512);
        b.HasOne(x => x.Question).WithMany(x => x.Answers).HasForeignKey(x => x.ProjectQuestionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorAppUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
