using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skill");
        builder.HasKey(s => s.SkillId);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.HasIndex(s => s.Name).IsUnique();
    }
}

public class CandidateSkillConfiguration : IEntityTypeConfiguration<CandidateSkill>
{
    public void Configure(EntityTypeBuilder<CandidateSkill> builder)
    {
        builder.ToTable("CandidateSkill");
        builder.HasKey(cs => new { cs.CandidateId, cs.SkillId });

        builder.HasOne(cs => cs.Candidate)
            .WithMany(c => c.Skills)
            .HasForeignKey(cs => cs.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cs => cs.Skill)
            .WithMany()
            .HasForeignKey(cs => cs.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProjectSkillConfiguration : IEntityTypeConfiguration<ProjectSkill>
{
    public void Configure(EntityTypeBuilder<ProjectSkill> builder)
    {
        builder.ToTable("ProjectSkill");
        builder.HasKey(ps => new { ps.ProjectId, ps.SkillId });

        builder.HasOne(ps => ps.Project)
            .WithMany(p => p.Skills)
            .HasForeignKey(ps => ps.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.Skill)
            .WithMany()
            .HasForeignKey(ps => ps.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
