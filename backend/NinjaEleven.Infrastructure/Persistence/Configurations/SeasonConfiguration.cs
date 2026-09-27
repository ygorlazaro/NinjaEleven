using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NinjaEleven.Domain.Seasons;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("seasons");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // The number is the identity. Two seasons in the same calendar year are two seasons,
        // and a season that outlives its own dates is still the season it was, so the year is
        // never what a season is identified by.
        builder.Property(s => s.Number).IsRequired();

        // "Temporada III". Derived from the number and stored so a season is one row and not
        // a number that has to be formatted everywhere it is shown.
        builder.Property(s => s.Name).HasMaxLength(60).IsRequired();
        builder.Property(s => s.StartDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.EndDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(s => s.Number).IsUnique();
        builder.HasIndex(s => s.Name).IsUnique();
    }
}
