using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class CommunityPostConfiguration : IEntityTypeConfiguration<CommunityPost>
{
    public void Configure(EntityTypeBuilder<CommunityPost> builder)
    {
        builder.ToTable("CommunityPost");
        builder.HasKey(p => p.CommunityPostId);
        builder.Property(p => p.Body).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.AuthorRoleLabel).HasMaxLength(50);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorAppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.CreatedUtc);
    }
}

public class CommunityCommentConfiguration : IEntityTypeConfiguration<CommunityComment>
{
    public void Configure(EntityTypeBuilder<CommunityComment> builder)
    {
        builder.ToTable("CommunityComment");
        builder.HasKey(c => c.CommunityCommentId);
        builder.Property(c => c.Body).HasMaxLength(1000).IsRequired();

        builder.HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.CommunityPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorAppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CommunityPostReactionConfiguration : IEntityTypeConfiguration<CommunityPostReaction>
{
    public void Configure(EntityTypeBuilder<CommunityPostReaction> builder)
    {
        builder.ToTable("CommunityPostReaction");
        builder.HasKey(r => new { r.CommunityPostId, r.AppUserId });

        builder.HasOne(r => r.Post)
            .WithMany(p => p.Reactions)
            .HasForeignKey(r => r.CommunityPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.AppUser)
            .WithMany()
            .HasForeignKey(r => r.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
