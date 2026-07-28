using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaunchPad.Infrastructure.Persistence.Configurations;

public class ProgramConfiguration : IEntityTypeConfiguration<Domain.Entities.Program>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Program> builder)
    {
        builder.ToTable("Program");
        builder.HasKey(p => p.ProgramId);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
    }
}
