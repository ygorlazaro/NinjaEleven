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
        builder.Property(s => s.Capacity).IsRequired().HasDefaultValue(5000);
        builder.Property(s => s.TicketPrice).HasPrecision(10, 2).IsRequired().HasDefaultValue(10m);

        builder.HasIndex(s => s.ClubId).IsUnique();
    }
}