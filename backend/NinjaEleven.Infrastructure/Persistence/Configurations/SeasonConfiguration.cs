using NinjaEleven.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("seasons");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(60).IsRequired();
        builder.Property(s => s.StartDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.EndDate).HasColumnType("date").IsRequired();
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(s => s.Name).IsUnique();
    }
}
