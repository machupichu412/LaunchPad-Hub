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
        builder.Property(p => p.ImageBlobPath).HasMaxLength(300);
        builder.Property(p => p.ImageContentType).HasMaxLength(100);
        builder.Property(p => p.LikeCount).HasDefaultValue(0);
        builder.Property(p => p.CommentCount).HasDefaultValue(0);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorAppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite, descending on both columns — required for correct, tie-break-safe
        // keyset pagination (see CommunityFeedCursor): a single-column index on CreatedUtc
        // alone can't guarantee stable ordering when two posts share the same timestamp.
        builder.HasIndex(p => new { p.CreatedUtc, p.CommunityPostId })
            .IsDescending(true, true);
    }
}

public class CommunityCommentConfiguration : IEntityTypeConfiguration<CommunityComment>
{
    public void Configure(EntityTypeBuilder<CommunityComment> builder)
    {
        builder.ToTable("CommunityComment");
        builder.HasKey(c => c.CommunityCommentId);
        builder.Property(c => c.Body).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.AuthorRoleLabel).HasMaxLength(50);

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

public class HashtagConfiguration : IEntityTypeConfiguration<Hashtag>
{
    public void Configure(EntityTypeBuilder<Hashtag> builder)
    {
        builder.ToTable("Hashtag");
        builder.HasKey(h => h.HashtagId);
        builder.Property(h => h.Tag).HasMaxLength(50).IsRequired();

        // Unique so GetOrCreateHashtagAsync's lookup is a plain indexed read, and so a
        // unique-constraint violation is the race guard for two concurrent posts
        // introducing the same brand-new tag at once.
        builder.HasIndex(h => h.Tag).IsUnique();
    }
}

public class CommunityPostHashtagConfiguration : IEntityTypeConfiguration<CommunityPostHashtag>
{
    public void Configure(EntityTypeBuilder<CommunityPostHashtag> builder)
    {
        builder.ToTable("CommunityPostHashtag");
        builder.HasKey(ph => new { ph.CommunityPostId, ph.HashtagId });

        builder.HasOne(ph => ph.Post)
            .WithMany(p => p.PostHashtags)
            .HasForeignKey(ph => ph.CommunityPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ph => ph.Hashtag)
            .WithMany(h => h.PostHashtags)
            .HasForeignKey(ph => ph.HashtagId)
            .OnDelete(DeleteBehavior.Restrict);

        // Leading with HashtagId (the PK above leads with CommunityPostId) so the
        // "?hashtag=" filter's "find every post for this tag" join is index-efficient in
        // that direction too, not just "find every tag for this post."
        builder.HasIndex(ph => new { ph.HashtagId, ph.CommunityPostId });
    }
}
