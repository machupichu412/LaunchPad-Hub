using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> builder)
    {
        builder.ToTable("Sponsor");
        builder.HasKey(s => s.SponsorId);
        builder.Property(s => s.Organization).HasMaxLength(200);
        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.RemovalReason).HasMaxLength(500);

        builder.HasOne(s => s.AppUser)
            .WithMany()
            .HasForeignKey(s => s.AppUserId);
    }
}
