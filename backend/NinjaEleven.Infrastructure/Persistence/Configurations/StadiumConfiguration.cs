using NinjaEleven.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NinjaEleven.Infrastructure.Persistence.Configurations;

public class StadiumConfiguration : IEntityTypeConfiguration<Stadium>
{
    public void Configure(EntityTypeBuilder<Stadium> builder)
    {
        builder.ToTable("stadiums");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.ClubId).IsRequired();

        // The name is stored, not composed. A ground a manager has renamed is a different
        // name from the one the club was founded with, and a screen that builds the name from
        // the club's would keep showing the old one after he changed it.
        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();

        builder.Property(s => s.Capacity).IsRequired().HasDefaultValue(Stadium.DefaultCapacity);
        builder.Property(s => s.TicketPrice)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasDefaultValue(Stadium.DefaultTicketPrice);

        builder.HasIndex(s => s.ClubId).IsUnique();
    }
}
