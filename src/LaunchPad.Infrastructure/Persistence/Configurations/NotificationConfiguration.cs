using LaunchPad.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.HasKey(n => n.NotificationId);
        builder.Property(n => n.Subject).HasMaxLength(300).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000).IsRequired();

        builder.HasOne(n => n.RecipientAppUser)
            .WithMany()
            .HasForeignKey(n => n.RecipientAppUserId);

        builder.HasIndex(n => new { n.RecipientAppUserId, n.IsRead });

        // The bell menu polls this every 30s per signed-in user (Header.tsx). The IsRead
        // index above can't serve the ordering, so the top-N read was sorting the user's
        // whole notification history on every poll.
        builder.HasIndex(n => new { n.RecipientAppUserId, n.CreatedUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notification_Recipient_Created");
    }
}
