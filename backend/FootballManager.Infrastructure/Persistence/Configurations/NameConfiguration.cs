using FootballManager.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FootballManager.Infrastructure.Persistence.Configurations;

public class NameConfiguration : IEntityTypeConfiguration<Name>
{
    public void Configure(EntityTypeBuilder<Name> builder)
    {
        builder.ToTable("names");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Value).HasMaxLength(80).IsRequired();
        builder.HasIndex(n => n.Value).IsUnique();
    }
}
