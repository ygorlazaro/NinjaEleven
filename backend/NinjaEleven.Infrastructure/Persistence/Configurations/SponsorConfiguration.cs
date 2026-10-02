using NinjaEleven.Domain.Sponsors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> builder)
    {
        builder.ToTable("sponsors");

        builder.HasKey(sponsor => sponsor.Id);
        builder.Property(sponsor => sponsor.Id).ValueGeneratedNever();

        builder.Property(sponsor => sponsor.Name).HasMaxLength(120).IsRequired();
        builder.Property(sponsor => sponsor.Industry).HasMaxLength(80).IsRequired();
        builder.Property(sponsor => sponsor.Color).HasMaxLength(9).IsRequired();
        builder.Property(sponsor => sponsor.Weight).IsRequired();
        builder.Property(sponsor => sponsor.MaxClubs).IsRequired();
        builder.Property(sponsor => sponsor.MinAppeal).IsRequired();
        builder.Property(sponsor => sponsor.MaxTier).IsRequired();

        builder.HasIndex(sponsor => sponsor.Name).IsUnique();
    }
}
