using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("divisions");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name).HasMaxLength(60).IsRequired();

        // The tier is the identity that matters: a division is "the third division", and a
        // name that is only a label must not be what two divisions are told apart by.
        builder.Property(d => d.Tier).IsRequired();

        builder.HasIndex(d => d.Tier).IsUnique();
        builder.HasIndex(d => d.Name).IsUnique();
    }
}
